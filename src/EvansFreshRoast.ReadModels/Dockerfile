FROM postgres

ENV POSTGRES_USER=read_models_user
ENV POSTGRES_DB=evans_fresh_roast_reads
ENV POSTGRES_PASSWORD=read_models_pass

COPY ./Migrations ./docker-entrypoint-initdb.d

EXPOSE 5432