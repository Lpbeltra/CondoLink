# Persistent attachment storage

The API stores every request attachment and temporary WhatsApp draft through
`LocalFileStorage`. Database rows contain relative keys such as
`requests/<request-id>/<generated-name>` and `whatsapp-drafts/<session-id>/<generated-name>`.

## Coolify

Create Persistent Storage for the API resource with:

- type: Volume;
- destination/mount path: `/app/data/attachments`;
- source: a named persistent volume managed by Coolify, or a persistent host
  directory dedicated to CondoLink attachments;
- environment variable: `FileStorage__RootPath=/app/data/attachments`.

Do not expose this directory through the frontend or a web server. Files remain
available only through the authenticated API endpoints.

The storage must be attached to every API instance during a rolling update. A
single-node local volume cannot be shared by containers scheduled on different
hosts; in that topology, use storage shared by those hosts before enabling more
than one API replica.

Existing database metadata does not contain file contents. Files missing from
the current container can only be recovered from the previous container layer,
an existing volume, or a backup, then copied into this mount while preserving
their relative keys.
