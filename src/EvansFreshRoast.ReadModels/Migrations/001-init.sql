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
