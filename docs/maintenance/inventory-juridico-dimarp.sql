-- READ-ONLY INVENTORY — DOES NOT MODIFY DATA
-- Target is intentionally fixed. This script contains SELECT/WITH only.
-- Do not treat category-only candidates as confirmed deletion targets.

WITH target_user AS (
    SELECT id, email, full_name, is_active, created_at, updated_at
    FROM users
    WHERE normalized_email = upper('juridico@dimarp.com')
), target_access AS (
    SELECT e.id AS access_id, e.user_id, e.management_company_id,
           e.job_title, e.access_type, e.is_active,
           e.created_at, e.updated_at, mc.name AS management_company_name
    FROM management_company_employees e
    JOIN target_user u ON u.id = e.user_id
    JOIN management_companies mc ON mc.id = e.management_company_id
), direct_request_ids AS (
    SELECT DISTINCT r.id AS request_id
    FROM management_company_requests r
    JOIN target_access a ON a.management_company_id = r.management_company_id
    WHERE r.created_by_user_id IN (SELECT id FROM target_user)
       OR r.acknowledged_by_user_id IN (SELECT id FROM target_user)
       OR r.completed_by_user_id IN (SELECT id FROM target_user)
       OR r.cancelled_by_user_id IN (SELECT id FROM target_user)
    UNION
    SELECT DISTINCT m.request_id
    FROM management_company_request_messages m
    WHERE m.author_user_id IN (SELECT id FROM target_user)
    UNION
    SELECT DISTINCT h.request_id
    FROM management_company_request_history h
    WHERE h.changed_by_user_id IN (SELECT id FROM target_user)
), category_request_ids AS (
    SELECT DISTINCT r.id AS request_id
    FROM management_company_requests r
    JOIN management_company_request_category_responsibles cr
      ON cr.category_id = r.category_id
    JOIN target_access a ON a.access_id = cr.access_id
), candidate_requests AS (
    SELECT request_id, 'DIRECT' AS evidence FROM direct_request_ids
    UNION
    SELECT request_id, 'CATEGORY_ONLY' AS evidence FROM category_request_ids
    WHERE request_id NOT IN (SELECT request_id FROM direct_request_ids)
)
SELECT u.id AS user_id, u.email, u.full_name, u.is_active,
       u.created_at, u.updated_at
FROM target_user u;

WITH target_user AS (
    SELECT id FROM users WHERE normalized_email = upper('juridico@dimarp.com')
)
SELECT e.id AS access_id, e.user_id, e.management_company_id,
       mc.name AS management_company_name, e.access_type, e.job_title,
       e.is_active, e.created_at, e.updated_at
FROM management_company_employees e
JOIN management_companies mc ON mc.id = e.management_company_id
JOIN target_user u ON u.id = e.user_id
ORDER BY mc.name, e.id;

WITH target_user AS (
    SELECT id FROM users WHERE normalized_email = upper('juridico@dimarp.com')
), target_access AS (
    SELECT id FROM management_company_employees
    WHERE user_id IN (SELECT id FROM target_user)
)
SELECT cr.id AS responsibility_id, cr.access_id, cr.category_id,
       c.name AS category_name, c.management_company_id,
       c.is_active AS category_is_active, cr.assigned_at
FROM management_company_request_category_responsibles cr
JOIN management_company_request_categories c ON c.id = cr.category_id
WHERE cr.access_id IN (SELECT id FROM target_access)
ORDER BY c.name, cr.id;

-- A: requests with direct participation by the target user.
WITH target_user AS (
    SELECT id FROM users WHERE normalized_email = upper('juridico@dimarp.com')
), direct_request_ids AS (
    SELECT r.id AS request_id, 'created_by_user' AS evidence
    FROM management_company_requests r
    WHERE r.created_by_user_id IN (SELECT id FROM target_user)
    UNION
    SELECT r.id, 'acknowledged_by_user'
    FROM management_company_requests r
    WHERE r.acknowledged_by_user_id IN (SELECT id FROM target_user)
    UNION
    SELECT r.id, 'completed_by_user'
    FROM management_company_requests r
    WHERE r.completed_by_user_id IN (SELECT id FROM target_user)
    UNION
    SELECT r.id, 'cancelled_by_user'
    FROM management_company_requests r
    WHERE r.cancelled_by_user_id IN (SELECT id FROM target_user)
    UNION
    SELECT m.request_id, 'message_author'
    FROM management_company_request_messages m
    WHERE m.author_user_id IN (SELECT id FROM target_user)
    UNION
    SELECT h.request_id, 'history_actor'
    FROM management_company_request_history h
    WHERE h.changed_by_user_id IN (SELECT id FROM target_user)
)
SELECT r.id AS request_id, r.friendly_identifier, r.type, r.status,
       r.condominium_id, co.name AS condominium_name,
       r.management_company_id, mc.name AS management_company_name,
       r.category_id, c.name AS category_name, r.created_at, r.updated_at,
       string_agg(DISTINCT d.evidence, ', ' ORDER BY d.evidence) AS evidence
FROM management_company_requests r
JOIN direct_request_ids d ON d.request_id = r.id
LEFT JOIN condominiums co ON co.id = r.condominium_id
LEFT JOIN management_companies mc ON mc.id = r.management_company_id
LEFT JOIN management_company_request_categories c ON c.id = r.category_id
GROUP BY r.id, co.name, mc.name, c.name
ORDER BY r.created_at, r.id;

-- B: category-only candidates. These are not direct participation evidence.
WITH target_user AS (
    SELECT id FROM users WHERE normalized_email = upper('juridico@dimarp.com')
), target_access AS (
    SELECT id FROM management_company_employees
    WHERE user_id IN (SELECT id FROM target_user)
), direct_requests AS (
    SELECT DISTINCT r.id AS request_id
    FROM management_company_requests r
    LEFT JOIN management_company_request_messages m ON m.request_id = r.id
    LEFT JOIN management_company_request_history h ON h.request_id = r.id
    WHERE r.created_by_user_id IN (SELECT id FROM target_user)
       OR r.acknowledged_by_user_id IN (SELECT id FROM target_user)
       OR r.completed_by_user_id IN (SELECT id FROM target_user)
       OR r.cancelled_by_user_id IN (SELECT id FROM target_user)
       OR m.author_user_id IN (SELECT id FROM target_user)
       OR h.changed_by_user_id IN (SELECT id FROM target_user)
)
SELECT DISTINCT r.id AS request_id, r.friendly_identifier, r.type, r.status,
       r.condominium_id, co.name AS condominium_name,
       r.management_company_id, mc.name AS management_company_name,
       r.category_id, c.name AS category_name, r.created_at, r.updated_at,
       'category_responsibility_only' AS evidence
FROM management_company_requests r
JOIN management_company_request_category_responsibles cr
  ON cr.category_id = r.category_id
JOIN target_access a ON a.id = cr.access_id
LEFT JOIN direct_requests d ON d.request_id = r.id
LEFT JOIN condominiums co ON co.id = r.condominium_id
LEFT JOIN management_companies mc ON mc.id = r.management_company_id
LEFT JOIN management_company_request_categories c ON c.id = r.category_id
WHERE d.request_id IS NULL
ORDER BY r.created_at, r.id;

-- Dependency counts for every request in either candidate group.
WITH target_user AS (
    SELECT id FROM users WHERE normalized_email = upper('juridico@dimarp.com')
), target_access AS (
    SELECT id FROM management_company_employees
    WHERE user_id IN (SELECT id FROM target_user)
), candidates AS (
    SELECT DISTINCT r.id AS request_id, 'DIRECT' AS evidence
    FROM management_company_requests r
    LEFT JOIN management_company_request_messages m ON m.request_id = r.id
    LEFT JOIN management_company_request_history h ON h.request_id = r.id
    WHERE r.created_by_user_id IN (SELECT id FROM target_user)
       OR r.acknowledged_by_user_id IN (SELECT id FROM target_user)
       OR r.completed_by_user_id IN (SELECT id FROM target_user)
       OR r.cancelled_by_user_id IN (SELECT id FROM target_user)
       OR m.author_user_id IN (SELECT id FROM target_user)
       OR h.changed_by_user_id IN (SELECT id FROM target_user)
    UNION
    SELECT DISTINCT r.id, 'CATEGORY_ONLY'
    FROM management_company_requests r
    JOIN management_company_request_category_responsibles cr ON cr.category_id = r.category_id
    JOIN target_access a ON a.id = cr.access_id
)
SELECT r.id AS request_id, r.friendly_identifier, c.evidence,
       (SELECT count(*) FROM management_company_request_messages x WHERE x.request_id = r.id) AS messages,
       (SELECT count(*) FROM management_company_request_history x WHERE x.request_id = r.id) AS events,
       (SELECT count(*) FROM management_company_request_attachments x WHERE x.request_id = r.id) AS attachments,
       (SELECT count(*) FROM notifications x WHERE x.management_company_request_id = r.id) AS notifications,
       (SELECT count(*) FROM management_company_payment_requests x WHERE x.request_id = r.id) AS payment_details,
       (SELECT count(*) FROM management_company_fine_requests x WHERE x.request_id = r.id) AS fine_details,
       (SELECT count(*) FROM management_company_general_question_requests x WHERE x.request_id = r.id) AS question_details,
       (SELECT count(*) FROM management_company_request_attachments x WHERE x.request_id = r.id AND x.purpose = 'PaymentBoleto') AS payment_boleto_attachments,
       (SELECT count(*) FROM management_company_request_attachments x WHERE x.request_id = r.id AND x.purpose = 'PaymentProof') AS payment_proof_attachments
FROM management_company_requests r
JOIN candidates c ON c.request_id = r.id
ORDER BY r.created_at, r.id, c.evidence;

-- Attachment metadata/storage keys for manual file verification; no content is read.
WITH target_user AS (
    SELECT id FROM users WHERE normalized_email = upper('juridico@dimarp.com')
), candidate_requests AS (
    SELECT DISTINCT r.id
    FROM management_company_requests r
    LEFT JOIN management_company_request_messages m ON m.request_id = r.id
    LEFT JOIN management_company_request_history h ON h.request_id = r.id
    WHERE r.created_by_user_id IN (SELECT id FROM target_user)
       OR r.acknowledged_by_user_id IN (SELECT id FROM target_user)
       OR r.completed_by_user_id IN (SELECT id FROM target_user)
       OR r.cancelled_by_user_id IN (SELECT id FROM target_user)
       OR m.author_user_id IN (SELECT id FROM target_user)
       OR h.changed_by_user_id IN (SELECT id FROM target_user)
)
SELECT a.id AS attachment_id, a.request_id, r.friendly_identifier,
       a.message_id, a.uploaded_by_user_id, a.purpose,
       a.original_file_name, a.storage_key, a.content_type, a.file_size, a.created_at
FROM management_company_request_attachments a
JOIN candidate_requests c ON c.id = a.request_id
JOIN management_company_requests r ON r.id = a.request_id
ORDER BY a.request_id, a.created_at, a.id;

-- All direct user references in request communication/authentication tables.
WITH target_user AS (
    SELECT id FROM users WHERE normalized_email = upper('juridico@dimarp.com')
)
SELECT 'request_messages.author_user_id' AS relation, count(*) AS quantity
FROM management_company_request_messages WHERE author_user_id IN (SELECT id FROM target_user)
UNION ALL SELECT 'request_history.changed_by_user_id', count(*)
FROM management_company_request_history WHERE changed_by_user_id IN (SELECT id FROM target_user)
UNION ALL SELECT 'request_attachments.uploaded_by_user_id', count(*)
FROM management_company_request_attachments WHERE uploaded_by_user_id IN (SELECT id FROM target_user)
UNION ALL SELECT 'notifications.recipient_user_id', count(*)
FROM notifications WHERE recipient_user_id IN (SELECT id FROM target_user)
UNION ALL SELECT 'refresh_sessions.user_id', count(*)
FROM refresh_sessions WHERE user_id IN (SELECT id FROM target_user);

-- Identity roles and other operational links. Secrets/tokens are intentionally omitted.
WITH target_user AS (
    SELECT id FROM users WHERE normalized_email = upper('juridico@dimarp.com')
)
SELECT r."Name" AS role_name, r."Id" AS role_id
FROM "AspNetUserRoles" ur
JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
JOIN target_user u ON u.id = ur."UserId"
ORDER BY r."Name";

WITH target_user AS (
    SELECT id FROM users WHERE normalized_email = upper('juridico@dimarp.com')
)
SELECT 'management_company_employees' AS relation, count(*) AS quantity
FROM management_company_employees WHERE user_id IN (SELECT id FROM target_user)
UNION ALL SELECT 'condominium_memberships', count(*)
FROM condominium_memberships WHERE user_id IN (SELECT id FROM target_user)
UNION ALL SELECT 'unit_memberships', count(*)
FROM unit_memberships WHERE user_id IN (SELECT id FROM target_user)
UNION ALL SELECT 'request_created_or_ack_completed_cancelled', count(*)
FROM management_company_requests
WHERE created_by_user_id IN (SELECT id FROM target_user)
   OR acknowledged_by_user_id IN (SELECT id FROM target_user)
   OR completed_by_user_id IN (SELECT id FROM target_user)
   OR cancelled_by_user_id IN (SELECT id FROM target_user)
UNION ALL SELECT 'request_messages', count(*)
FROM management_company_request_messages WHERE author_user_id IN (SELECT id FROM target_user)
UNION ALL SELECT 'request_history', count(*)
FROM management_company_request_history WHERE changed_by_user_id IN (SELECT id FROM target_user)
UNION ALL SELECT 'refresh_sessions', count(*)
FROM refresh_sessions WHERE user_id IN (SELECT id FROM target_user);

-- Refresh-session metadata only.
WITH target_user AS (
    SELECT id FROM users WHERE normalized_email = upper('juridico@dimarp.com')
)
SELECT id AS refresh_session_id, user_id, created_at, expires_at, last_used_at, revoked_at,
       replaced_by_session_id
FROM refresh_sessions
WHERE user_id IN (SELECT id FROM target_user)
ORDER BY created_at, id;

-- Final compact summary. Values are evidence/reporting only; no deletion decision is made.
WITH target_user AS (
    SELECT id FROM users WHERE normalized_email = upper('juridico@dimarp.com')
), target_access AS (
    SELECT id FROM management_company_employees WHERE user_id IN (SELECT id FROM target_user)
), direct_requests AS (
    SELECT DISTINCT r.id
    FROM management_company_requests r
    LEFT JOIN management_company_request_messages m ON m.request_id = r.id
    LEFT JOIN management_company_request_history h ON h.request_id = r.id
    WHERE r.created_by_user_id IN (SELECT id FROM target_user)
       OR r.acknowledged_by_user_id IN (SELECT id FROM target_user)
       OR r.completed_by_user_id IN (SELECT id FROM target_user)
       OR r.cancelled_by_user_id IN (SELECT id FROM target_user)
       OR m.author_user_id IN (SELECT id FROM target_user)
       OR h.changed_by_user_id IN (SELECT id FROM target_user)
), category_requests AS (
    SELECT DISTINCT r.id
    FROM management_company_requests r
    JOIN management_company_request_category_responsibles cr ON cr.category_id = r.category_id
    JOIN target_access a ON a.id = cr.access_id
), other_links AS (
    SELECT count(*)::bigint AS quantity FROM management_company_employees
    WHERE user_id IN (SELECT id FROM target_user)
      AND id NOT IN (SELECT id FROM target_access)
    UNION ALL SELECT count(*) FROM condominium_memberships WHERE user_id IN (SELECT id FROM target_user)
    UNION ALL SELECT count(*) FROM unit_memberships WHERE user_id IN (SELECT id FROM target_user)
    UNION ALL SELECT count(*) FROM "AspNetUserRoles" WHERE "UserId" IN (SELECT id FROM target_user)
)
SELECT (SELECT count(*) FROM target_user) AS user_found,
       (SELECT count(*) FROM target_access) AS access_count,
       (SELECT count(*) FROM direct_requests) AS direct_request_count,
       (SELECT count(*) FROM category_requests WHERE id NOT IN (SELECT id FROM direct_requests)) AS category_only_request_count,
       (SELECT count(*) FROM management_company_request_category_responsibles WHERE access_id IN (SELECT id FROM target_access)) AS responsibility_count,
       (SELECT count(*) FROM management_company_request_messages WHERE author_user_id IN (SELECT id FROM target_user)) AS user_message_count,
       (SELECT count(*) FROM management_company_request_history WHERE changed_by_user_id IN (SELECT id FROM target_user)) AS user_event_count,
       (SELECT count(*) FROM management_company_request_attachments WHERE request_id IN (SELECT id FROM direct_requests)) AS direct_attachment_count,
       (SELECT count(*) FROM refresh_sessions WHERE user_id IN (SELECT id FROM target_user)) AS refresh_session_count,
       (SELECT coalesce(sum(quantity), 0) FROM other_links) AS total_known_user_links,
       CASE WHEN (SELECT coalesce(sum(quantity), 0) FROM other_links) > 0 THEN 'SIM' ELSE 'NAO' END AS possui_outros_vinculos;
