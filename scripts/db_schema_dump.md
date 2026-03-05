# DB schema dump (Postgres)

```bash
pg_dump --schema-only --no-owner --no-privileges \
  -h <HOST> -p <PORT> -U <USER> -d <DBNAME> > schema.sql
```
