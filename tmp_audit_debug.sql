SELECT "ActivityId", "ActivityType", "Details", "ActivityDate"
FROM "DocumentActivity"
WHERE "ActivityType" IN ('user-created', 'user-password-reset', 'user-updated')
ORDER BY "ActivityId" DESC
LIMIT 20;
