CREATE TABLE customers(
    customer_id UUID NOT NULL PRIMARY KEY
  , customer_data JSONB NOT NULL
);

CREATE TABLE coffees(
    coffee_id UUID NOT NULL PRIMARY KEY
  , coffee_data JSONB NOT NULL
)
