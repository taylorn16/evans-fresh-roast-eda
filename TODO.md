- JWT Authentication
- Incoming SMS webhook/parsing
- handler wrapper for incoming ids??
- roast repository get functions
- roast read model event consumer
- rename and reorganize Utils project
- flesh out rest of customers api
- Error handling/messaging/logging -- basically everywhere!
- Split up roast domain file into several modules

## Roasts API

Roast Lifecycle (Statuses):
1. Created (Unpublished? Draft?)
    - Has name, dates at minimum
    - Can add/remove coffees and customers freely
    - Orders can't be placed
2. Open
    - Can't add/remove coffees from a roast
    - Can't change roast dates or 
3. Closed
    - Read-only (no updating coffees, customers, name, or dates)
    - Orders can't be placed
    - Invoices can be marked paid, that's it

POST /api/v1/roasts
- Create a new roast
- Can't create a new roast while any other roast is `Open`

GET /api/v1/roasts
- Returns summary information for all roasts
    - name
    - dates
    - count of customers
    - coffee names included
    - status
- Paginated?? (maybe not to start with?)

GET /api/v1/roasts/{roast_id}
- Get a specific roast
- Detailed view (customers, coffees, orders & associated invoices)

PUT /api/v1/roasts/{roast_id}
- update the name, roast date, and order-by date
- can only update dates before a roast is in-flight

POST /api/v1/roasts/{roast_id}/open
- sends a text blast to all subscribed customers
- probably maps to an `OpenRoast` command that changes the status to `Open`
- can only send once?

POST /api/v1/roasts/{roast_id}/follow-up
- send a reminder text blast to all subscribed customers who haven't placed an order yet
- keep track of how many times this operation has been done?

POST /api/v1/roasts/{roast_id}/completion
- sends a roast closed text blast to all customers who placed an order
    - text varies based on whether their order was marked as paid yet or not

(PUT/DELETE) /api/v1/roasts/{roast_id}/coffees
- add/remove coffee selections to/from a roast
- can only do this before a roast is in-flight

(PUT/DELETE) /api/v1/roasts/{roast_id}/contacts
- add/remove contacts to/from a roast
- can only do this before a roast is in-flight

PUT /api/v1/roasts/{roast_id}/customers/{customer_id}/invoice
- mark invoice paid for a customer with a payment method

## Incoming SMS Webhook Mappings

### "SUBSCRIBE"

- Corresponds to a Customer.Subscribed event
- Subscribe to text notifications
- Can do this any time

### "UNSUBSCRIBE"

- Corresponds to a Customer.Unsubscribed event
- Unsubscribe from receiving text notifications
- Still will receive texts related to open orders
- Can do this any time

### "CANCEL"

- Cancel an open order on the open roast (if one exists)
- Text should give the customer the option to place a new order if they want
- Can't cancel once a roast is closed; should tell them to reach out to evan directly

### "CONFIRM"

- Confirm an unconfirmed order on the open roast (if one exists)
- Essentially creates an invoice for the order
- Text should give them an option to cancel

### Ordering

- In the format, e.g. (newlines)
2 A
1 B
2 D
- OR (semicolons)
2 A; 1 B; 2 D

- Text will respond with a price/summary and ask them to confirm
- When the roast is closed, a notification text will be sent
