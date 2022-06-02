# communications-management
Web app to manage press communications on clubs.

Use docker compose to start a container with the event store db.

The test suite requires docker to run, as it creates (and then deletes) a container per end to end test.

The appsettings also require a primary admin email, add it on `appsettings.development.json`.