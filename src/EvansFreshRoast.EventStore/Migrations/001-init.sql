CREATE TABLE events(
    aggregate_name VARCHAR(100) NOT NULL
  , aggregate_id UUID NOT NULL
  , aggregate_version BIGINT NOT NULL
  , created_timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
  , event_payload TEXT NOT NULL
  , event_name VARCHAR(100) NOT NULL
  , event_id UUID NOT NULL
  , PRIMARY KEY(aggregate_name, aggregate_id, aggregate_version)
);
