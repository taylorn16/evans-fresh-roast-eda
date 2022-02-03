namespace EvansFreshRoast.Auth

open System
open System.Text
open System.Security.Claims
open System.IdentityModel.Tokens.Jwt
open Microsoft.IdentityModel.Tokens
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Utils
open NodaTime
open Npgsql.FSharp

type UserName = private UserName of String100
module UserName =
    let create =
        String100.create
        >> Result.map UserName

    let apply f (UserName s) = String100.apply f s

    let value = apply id

type LoginCode = private LoginCode of string
module LoginCode =
    let private rnd = Random()

    let create () =
        seq { for _ in 1..9 do rnd.Next(10) |> string }
        |> Seq.fold (+) ""
        |> LoginCode

    let makeUnsafe s = LoginCode s

    let value (LoginCode code) = code

type User =
    { Id: Id<User>
      Name: UserName
      PhoneNumber: UsPhoneNumber }

type UserLogin =
    { Id: Id<UserLogin>
      Code: LoginCode
      ExpiresAt: OffsetDateTime
      HasBeenUsed: bool }

[<CLIMutable>]
type JwtConfig =
    { SecretKey: string
      Issuer: string
      Audience: string
      ExpirationInMinutes: int }

type Jwt = private Jwt of string
module Jwt =
    let create (conf: JwtConfig) (userId: Id<User>) =
        let key = SymmetricSecurityKey(Encoding.UTF8.GetBytes(conf.SecretKey))
        let creds = SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        let expirationDt = DateTime.Now.AddMinutes(conf.ExpirationInMinutes)

        let token = JwtSecurityToken(
            signingCredentials = creds,
            expires = expirationDt,
            issuer = conf.Issuer,
            audience = conf.Audience,
            claims = [ new Claim(ClaimTypes.NameIdentifier, userId.ToString()) ])

        JwtSecurityTokenHandler().WriteToken(token)

module Repository =
    let getUser connectionString phoneNumber = async {
        try
            return! ConnectionString.value connectionString
            |> Sql.connect
            |> Sql.query
                """
                SELECT
                    user_id
                  , user_name
                  , user_phone_number
                FROM users
                WHERE user_phone_number = @phoneNumber
                LIMIT 1
                """
            |> Sql.parameters
                [ "phoneNumber", phoneNumber |> UsPhoneNumber.value |> Sql.string ]
            |> Sql.executeAsync (fun r ->
                { Id = r.uuid "user_id" |> Id.create |> unsafeAssertOk
                  Name = r.string "user_name" |> UserName.create |> unsafeAssertOk
                  PhoneNumber = phoneNumber })
            |> Async.AwaitTask
            |> Async.map (
                List.tryHead
                >> Result.ofOption (exn("Unable to find user."))
            )
        with
        | ex ->
            return Error <| exn("Error querying user.", ex)
    }

    let getUserLogin connectionString (id: Id<UserLogin>) = async {
        try
            return! ConnectionString.value connectionString
            |> Sql.connect
            |> Sql.query
                """
                SELECT
                    login_code
                  , login_code_expiration
                  , is_validated
                FROM user_logins
                WHERE user_login_id = @userLoginId
                LIMIT 1
                """
            |> Sql.parameters
                [ "userLoginId", id |> Id.value |> Sql.uuid ]
            |> Sql.executeAsync (fun r ->
                { Id = id
                  Code = r.string "login_code" |> LoginCode.makeUnsafe
                  HasBeenUsed = r.bool "is_validated"
                  ExpiresAt = OffsetDateTime.FromDateTimeOffset(
                      r.datetimeOffset "login_code_expiration") })
            |> Async.AwaitTask
            |> Async.map (
                List.tryHead
                >> Result.ofOption (exn("Unable to find user login."))
            )
        with
        | ex ->
            return Error <| exn("Error querying user login.", ex)
    }

    let createUserLogin connectionString (userId: Id<User>) = async {
        let userLoginId: Id<UserLogin> = Id.newId()
        let loginCode = LoginCode.create()

        try
            return! ConnectionString.value connectionString
            |> Sql.connect
            |> Sql.query
                """
                INSERT INTO user_logins (
                    user_login_id
                  , login_code
                  , login_code_expiration
                  , user_fk
                ) VALUES (
                    @userLoginId
                  , @loginCode
                  , @expiration
                  , @userId
                )
                """
            |> Sql.parameters
                [ "userLoginId", userLoginId |> Id.value |> Sql.uuid
                  "loginCode", loginCode |> LoginCode.value |> Sql.string
                  "expiration", DateTimeOffset.Now.AddMinutes(10.) |> Sql.timestamptz
                  "userId", userId |> Id.value |> Sql.uuid ]
            |> Sql.executeNonQueryAsync
            |> Async.AwaitTask
            |> Async.Ignore
            |> Async.map (fun _ -> Ok (userLoginId, loginCode))
        with
        | ex ->
            return Error <| exn("Error creating user login.", ex)
    }

    let validateUserLogin connectionString (userLoginId: Id<UserLogin>) = async {
        try
            return! ConnectionString.value connectionString
            |> Sql.connect
            |> Sql.query
                """
                UPDATE user_logins
                SET is_validated = 1::BIT
                WHERE user_login_id = @userLoginId
                RETURNING user_fk
                """
            |> Sql.parameters
                [ "userLoginId", userLoginId |> Id.value |> Sql.uuid ]
            |> Sql.executeAsync (fun r ->
                r.uuid "user_fk"
                |> Id.create
                |> unsafeAssertOk
                :> Id<User>)
            |> Async.AwaitTask
            |> Async.map (
                List.head
                >> Ok
            )
        with
        | ex ->
            return Error <| exn("Error marking user login validated.", ex)
    }
