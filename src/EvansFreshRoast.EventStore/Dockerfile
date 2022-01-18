FROM postgres

ENV POSTGRES_USER=event_store_user
ENV POSTGRES_DB=evans_fresh_roast_events
ENV POSTGRES_PASSWORD=event_store_pass

COPY ./Migrations ./docker-entrypoint-initdb.d

EXPOSE 5432