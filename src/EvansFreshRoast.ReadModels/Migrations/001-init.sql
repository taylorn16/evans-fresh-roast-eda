CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE customers(
    customer_id UUID NOT NULL PRIMARY KEY
  , customer_data JSONB NOT NULL
);

CREATE TABLE coffees(
    coffee_id UUID NOT NULL PRIMARY KEY
  , coffee_data JSONB NOT NULL
);

CREATE TABLE invoices(
    invoice_id BIGSERIAL NOT NULL PRIMARY KEY
  , invoice_amount DECIMAL(15, 2) NOT NULL
  , payment_method TEXT NULL
);

CREATE TABLE roasts(
    roast_id UUID NOT NULL PRIMARY KEY
  , roast_name TEXT NOT NULL
  , roast_date DATE NOT NULL
  , order_by_date DATE NOT NULL
  , customer_ids UUID[] NOT NULL
  , coffee_ids UUID[] NOT NULL
  , roast_status TEXT NOT NULL
  , reminders_sent_count INT NOT NULL DEFAULT 0
);

CREATE TABLE orders(
    order_id BIGSERIAL NOT NULL PRIMARY KEY
  , customer_id UUID NOT NULL
  , placed_time TIMESTAMPTZ NOT NULL
  , invoice_fk BIGINT NULL
  , roast_fk UUID NOT NULL
  , CONSTRAINT fk_invoice FOREIGN KEY(invoice_fk) REFERENCES invoices(invoice_id)
  , CONSTRAINT fk_roast FOREIGN KEY(roast_fk) REFERENCES roasts(roast_id)
);

CREATE TABLE order_line_items(
    order_line_item_id BIGSERIAL NOT NULL PRIMARY KEY
  , order_fk BIGINT NOT NULL
  , coffee_id UUID NOT NULL
  , quantity INT NOT NULL
  , CONSTRAINT fk_order FOREIGN KEY(order_fk) REFERENCES orders(order_id)
);

CREATE TABLE users(
    user_id UUID NOT NULL PRIMARY KEY DEFAULT uuid_generate_v4()
  , user_name TEXT NOT NULL
  , user_phone_number VARCHAR(12) NOT NULL
);

CREATE INDEX idx_user_phn ON users(user_phone_number);

CREATE TABLE user_logins(
    user_login_id UUID NOT NULL PRIMARY KEY
  , login_code VARCHAR(9) NOT NULL
  , login_code_expiration TIMESTAMPTZ NOT NULL
  , user_fk UUID NOT NULL
  , is_validated BIT NOT NULL DEFAULT 0::BIT
  , CONSTRAINT fk_user FOREIGN KEY(user_fk) REFERENCES users(user_id)
);

-- INSERT INTO users(user_name, user_phone_number)
-- VALUES('', '');
