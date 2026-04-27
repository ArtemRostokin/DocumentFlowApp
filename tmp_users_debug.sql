SELECT "UserId", "UserName", "Email", "IsActive", length("PasswordHash") AS hash_len, left("PasswordHash", 16) AS hash_prefix, "LastLogin"
FROM "Users"
ORDER BY "UserId" DESC
LIMIT 10;
