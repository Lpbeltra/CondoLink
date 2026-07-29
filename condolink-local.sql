--
-- PostgreSQL database dump
--

\restrict Z7KRZSZ0vfRlCIuiU2oxbyUkzAcQ9bbT9ccCq5N6eJ5m2EUps6w2Ufzcb8y3oyB

-- Dumped from database version 17.10 (Debian 17.10-1.pgdg13+1)
-- Dumped by pg_dump version 17.10 (Debian 17.10-1.pgdg13+1)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

ALTER TABLE IF EXISTS ONLY public.whatsapp_sessions DROP CONSTRAINT IF EXISTS "FK_whatsapp_sessions_users_user_id";
ALTER TABLE IF EXISTS ONLY public.whatsapp_sessions DROP CONSTRAINT IF EXISTS "FK_whatsapp_sessions_units_unit_id";
ALTER TABLE IF EXISTS ONLY public.whatsapp_sessions DROP CONSTRAINT IF EXISTS "FK_whatsapp_sessions_requests_request_id";
ALTER TABLE IF EXISTS ONLY public.whatsapp_sessions DROP CONSTRAINT IF EXISTS "FK_whatsapp_sessions_condominiums_condominium_id";
ALTER TABLE IF EXISTS ONLY public.whatsapp_sessions DROP CONSTRAINT IF EXISTS "FK_whatsapp_sessions_categories_category_id";
ALTER TABLE IF EXISTS ONLY public.whatsapp_outbound_messages DROP CONSTRAINT IF EXISTS "FK_whatsapp_outbound_messages_users_user_id";
ALTER TABLE IF EXISTS ONLY public.whatsapp_outbound_messages DROP CONSTRAINT IF EXISTS "FK_whatsapp_outbound_messages_requests_request_id";
ALTER TABLE IF EXISTS ONLY public.whatsapp_outbound_messages DROP CONSTRAINT IF EXISTS "FK_whatsapp_outbound_messages_request_messages_request_message~";
ALTER TABLE IF EXISTS ONLY public.whatsapp_outbound_messages DROP CONSTRAINT IF EXISTS "FK_whatsapp_outbound_messages_condominiums_condominium_id";
ALTER TABLE IF EXISTS ONLY public.whatsapp_inbound_messages DROP CONSTRAINT IF EXISTS "FK_whatsapp_inbound_messages_users_identified_user_id";
ALTER TABLE IF EXISTS ONLY public.whatsapp_draft_attachments DROP CONSTRAINT IF EXISTS "FK_whatsapp_draft_attachments_whatsapp_sessions_session_id";
ALTER TABLE IF EXISTS ONLY public.units DROP CONSTRAINT IF EXISTS "FK_units_condominiums_condominium_id";
ALTER TABLE IF EXISTS ONLY public.units DROP CONSTRAINT IF EXISTS "FK_units_condominium_blocks_block_id";
ALTER TABLE IF EXISTS ONLY public.unit_memberships DROP CONSTRAINT IF EXISTS "FK_unit_memberships_users_user_id";
ALTER TABLE IF EXISTS ONLY public.unit_memberships DROP CONSTRAINT IF EXISTS "FK_unit_memberships_units_unit_id";
ALTER TABLE IF EXISTS ONLY public.requests DROP CONSTRAINT IF EXISTS "FK_requests_users_author_user_id";
ALTER TABLE IF EXISTS ONLY public.requests DROP CONSTRAINT IF EXISTS "FK_requests_units_target_unit_id";
ALTER TABLE IF EXISTS ONLY public.requests DROP CONSTRAINT IF EXISTS "FK_requests_condominiums_condominium_id";
ALTER TABLE IF EXISTS ONLY public.requests DROP CONSTRAINT IF EXISTS "FK_requests_categories_category_id";
ALTER TABLE IF EXISTS ONLY public.request_status_history DROP CONSTRAINT IF EXISTS "FK_request_status_history_users_changed_by_user_id";
ALTER TABLE IF EXISTS ONLY public.request_status_history DROP CONSTRAINT IF EXISTS "FK_request_status_history_requests_request_id";
ALTER TABLE IF EXISTS ONLY public.request_messages DROP CONSTRAINT IF EXISTS "FK_request_messages_users_author_user_id";
ALTER TABLE IF EXISTS ONLY public.request_messages DROP CONSTRAINT IF EXISTS "FK_request_messages_requests_request_id";
ALTER TABLE IF EXISTS ONLY public.request_attachments DROP CONSTRAINT IF EXISTS "FK_request_attachments_users_uploaded_by_user_id";
ALTER TABLE IF EXISTS ONLY public.request_attachments DROP CONSTRAINT IF EXISTS "FK_request_attachments_requests_request_id";
ALTER TABLE IF EXISTS ONLY public.request_attachments DROP CONSTRAINT IF EXISTS "FK_request_attachments_request_messages_request_message_id";
ALTER TABLE IF EXISTS ONLY public.notifications DROP CONSTRAINT IF EXISTS "FK_notifications_users_recipient_user_id";
ALTER TABLE IF EXISTS ONLY public.notifications DROP CONSTRAINT IF EXISTS "FK_notifications_requests_request_id";
ALTER TABLE IF EXISTS ONLY public.notifications DROP CONSTRAINT IF EXISTS "FK_notifications_condominiums_condominium_id";
ALTER TABLE IF EXISTS ONLY public.management_company_request_categories DROP CONSTRAINT IF EXISTS "FK_management_company_request_categories_management_companies_~";
ALTER TABLE IF EXISTS ONLY public.management_company_employees DROP CONSTRAINT IF EXISTS "FK_management_company_employees_users_user_id";
ALTER TABLE IF EXISTS ONLY public.management_company_employees DROP CONSTRAINT IF EXISTS "FK_management_company_employees_management_companies_managemen~";
ALTER TABLE IF EXISTS ONLY public.condominiums DROP CONSTRAINT IF EXISTS "FK_condominiums_management_companies_management_company_id";
ALTER TABLE IF EXISTS ONLY public.condominium_memberships DROP CONSTRAINT IF EXISTS "FK_condominium_memberships_users_user_id";
ALTER TABLE IF EXISTS ONLY public.condominium_memberships DROP CONSTRAINT IF EXISTS "FK_condominium_memberships_condominiums_condominium_id";
ALTER TABLE IF EXISTS ONLY public.condominium_membership_roles DROP CONSTRAINT IF EXISTS "FK_condominium_membership_roles_condominium_memberships_condom~";
ALTER TABLE IF EXISTS ONLY public.condominium_blocks DROP CONSTRAINT IF EXISTS "FK_condominium_blocks_condominiums_condominium_id";
ALTER TABLE IF EXISTS ONLY public.categories DROP CONSTRAINT IF EXISTS "FK_categories_condominiums_condominium_id";
ALTER TABLE IF EXISTS ONLY public."AspNetUserTokens" DROP CONSTRAINT IF EXISTS "FK_AspNetUserTokens_users_UserId";
ALTER TABLE IF EXISTS ONLY public."AspNetUserRoles" DROP CONSTRAINT IF EXISTS "FK_AspNetUserRoles_users_UserId";
ALTER TABLE IF EXISTS ONLY public."AspNetUserRoles" DROP CONSTRAINT IF EXISTS "FK_AspNetUserRoles_AspNetRoles_RoleId";
ALTER TABLE IF EXISTS ONLY public."AspNetUserLogins" DROP CONSTRAINT IF EXISTS "FK_AspNetUserLogins_users_UserId";
ALTER TABLE IF EXISTS ONLY public."AspNetUserClaims" DROP CONSTRAINT IF EXISTS "FK_AspNetUserClaims_users_UserId";
ALTER TABLE IF EXISTS ONLY public."AspNetRoleClaims" DROP CONSTRAINT IF EXISTS "FK_AspNetRoleClaims_AspNetRoles_RoleId";
DROP INDEX IF EXISTS public.ux_whatsapp_sessions_phone_number;
DROP INDEX IF EXISTS public.ux_whatsapp_outbound_idempotency_key;
DROP INDEX IF EXISTS public.ux_whatsapp_outbound_external_message_id;
DROP INDEX IF EXISTS public.ux_whatsapp_inbound_messages_external_id;
DROP INDEX IF EXISTS public.ux_whatsapp_draft_attachments_external_media_id;
DROP INDEX IF EXISTS public.ux_users_normalized_email;
DROP INDEX IF EXISTS public.ux_users_manager_cpf;
DROP INDEX IF EXISTS public.ux_users_manager_cnpj;
DROP INDEX IF EXISTS public.ux_units_condominium_identifier_without_block_id;
DROP INDEX IF EXISTS public.ux_units_block_identifier;
DROP INDEX IF EXISTS public.ux_unit_memberships_user_unit_relationship;
DROP INDEX IF EXISTS public.ux_management_company_request_categories_company_normalized_nam;
DROP INDEX IF EXISTS public.ux_management_company_employees_user_id;
DROP INDEX IF EXISTS public.ux_management_companies_email;
DROP INDEX IF EXISTS public.ux_management_companies_cnpj;
DROP INDEX IF EXISTS public.ux_condominiums_cnpj;
DROP INDEX IF EXISTS public.ux_condominium_memberships_user_condominium;
DROP INDEX IF EXISTS public.ux_condominium_membership_roles_membership_role;
DROP INDEX IF EXISTS public.ux_condominium_blocks_condominium_identifier;
DROP INDEX IF EXISTS public.ux_categories_condominium_normalized_name;
DROP INDEX IF EXISTS public.ix_management_company_employees_management_company_id;
DROP INDEX IF EXISTS public.ix_condominiums_management_company_id;
DROP INDEX IF EXISTS public."UserNameIndex";
DROP INDEX IF EXISTS public."RoleNameIndex";
DROP INDEX IF EXISTS public."IX_whatsapp_sessions_user_id";
DROP INDEX IF EXISTS public."IX_whatsapp_sessions_unit_id";
DROP INDEX IF EXISTS public."IX_whatsapp_sessions_request_id";
DROP INDEX IF EXISTS public."IX_whatsapp_sessions_condominium_id";
DROP INDEX IF EXISTS public."IX_whatsapp_sessions_category_id";
DROP INDEX IF EXISTS public."IX_whatsapp_outbound_messages_user_id";
DROP INDEX IF EXISTS public."IX_whatsapp_outbound_messages_status_next_attempt_at";
DROP INDEX IF EXISTS public."IX_whatsapp_outbound_messages_request_message_id";
DROP INDEX IF EXISTS public."IX_whatsapp_outbound_messages_request_id";
DROP INDEX IF EXISTS public."IX_whatsapp_outbound_messages_condominium_id_created_at";
DROP INDEX IF EXISTS public."IX_whatsapp_inbound_messages_identified_user_id";
DROP INDEX IF EXISTS public."IX_whatsapp_draft_attachments_session_id_created_at";
DROP INDEX IF EXISTS public."IX_unit_memberships_unit_id";
DROP INDEX IF EXISTS public."IX_requests_target_unit_id";
DROP INDEX IF EXISTS public."IX_requests_status";
DROP INDEX IF EXISTS public."IX_requests_created_at";
DROP INDEX IF EXISTS public."IX_requests_condominium_id";
DROP INDEX IF EXISTS public."IX_requests_category_id";
DROP INDEX IF EXISTS public."IX_requests_author_user_id";
DROP INDEX IF EXISTS public."IX_request_status_history_request_id_created_at";
DROP INDEX IF EXISTS public."IX_request_status_history_changed_by_user_id";
DROP INDEX IF EXISTS public."IX_request_messages_request_id_created_at";
DROP INDEX IF EXISTS public."IX_request_messages_author_user_id";
DROP INDEX IF EXISTS public."IX_request_attachments_uploaded_by_user_id";
DROP INDEX IF EXISTS public."IX_request_attachments_request_message_id";
DROP INDEX IF EXISTS public."IX_request_attachments_request_id_created_at";
DROP INDEX IF EXISTS public."IX_notifications_request_id";
DROP INDEX IF EXISTS public."IX_notifications_recipient_user_id_read_at";
DROP INDEX IF EXISTS public."IX_notifications_recipient_user_id_condominium_id_created_at";
DROP INDEX IF EXISTS public."IX_notifications_condominium_id";
DROP INDEX IF EXISTS public."IX_condominium_memberships_condominium_id";
DROP INDEX IF EXISTS public."IX_AspNetUserRoles_RoleId";
DROP INDEX IF EXISTS public."IX_AspNetUserLogins_UserId";
DROP INDEX IF EXISTS public."IX_AspNetUserClaims_UserId";
DROP INDEX IF EXISTS public."IX_AspNetRoleClaims_RoleId";
ALTER TABLE IF EXISTS ONLY public.whatsapp_sessions DROP CONSTRAINT IF EXISTS "PK_whatsapp_sessions";
ALTER TABLE IF EXISTS ONLY public.whatsapp_outbound_messages DROP CONSTRAINT IF EXISTS "PK_whatsapp_outbound_messages";
ALTER TABLE IF EXISTS ONLY public.whatsapp_inbound_messages DROP CONSTRAINT IF EXISTS "PK_whatsapp_inbound_messages";
ALTER TABLE IF EXISTS ONLY public.whatsapp_draft_attachments DROP CONSTRAINT IF EXISTS "PK_whatsapp_draft_attachments";
ALTER TABLE IF EXISTS ONLY public.users DROP CONSTRAINT IF EXISTS "PK_users";
ALTER TABLE IF EXISTS ONLY public.units DROP CONSTRAINT IF EXISTS "PK_units";
ALTER TABLE IF EXISTS ONLY public.unit_memberships DROP CONSTRAINT IF EXISTS "PK_unit_memberships";
ALTER TABLE IF EXISTS ONLY public.requests DROP CONSTRAINT IF EXISTS "PK_requests";
ALTER TABLE IF EXISTS ONLY public.request_status_history DROP CONSTRAINT IF EXISTS "PK_request_status_history";
ALTER TABLE IF EXISTS ONLY public.request_messages DROP CONSTRAINT IF EXISTS "PK_request_messages";
ALTER TABLE IF EXISTS ONLY public.request_attachments DROP CONSTRAINT IF EXISTS "PK_request_attachments";
ALTER TABLE IF EXISTS ONLY public.notifications DROP CONSTRAINT IF EXISTS "PK_notifications";
ALTER TABLE IF EXISTS ONLY public.management_company_request_categories DROP CONSTRAINT IF EXISTS "PK_management_company_request_categories";
ALTER TABLE IF EXISTS ONLY public.management_company_employees DROP CONSTRAINT IF EXISTS "PK_management_company_employees";
ALTER TABLE IF EXISTS ONLY public.management_companies DROP CONSTRAINT IF EXISTS "PK_management_companies";
ALTER TABLE IF EXISTS ONLY public.condominiums DROP CONSTRAINT IF EXISTS "PK_condominiums";
ALTER TABLE IF EXISTS ONLY public.condominium_memberships DROP CONSTRAINT IF EXISTS "PK_condominium_memberships";
ALTER TABLE IF EXISTS ONLY public.condominium_membership_roles DROP CONSTRAINT IF EXISTS "PK_condominium_membership_roles";
ALTER TABLE IF EXISTS ONLY public.condominium_blocks DROP CONSTRAINT IF EXISTS "PK_condominium_blocks";
ALTER TABLE IF EXISTS ONLY public.categories DROP CONSTRAINT IF EXISTS "PK_categories";
ALTER TABLE IF EXISTS ONLY public."__EFMigrationsHistory" DROP CONSTRAINT IF EXISTS "PK___EFMigrationsHistory";
ALTER TABLE IF EXISTS ONLY public."AspNetUserTokens" DROP CONSTRAINT IF EXISTS "PK_AspNetUserTokens";
ALTER TABLE IF EXISTS ONLY public."AspNetUserRoles" DROP CONSTRAINT IF EXISTS "PK_AspNetUserRoles";
ALTER TABLE IF EXISTS ONLY public."AspNetUserLogins" DROP CONSTRAINT IF EXISTS "PK_AspNetUserLogins";
ALTER TABLE IF EXISTS ONLY public."AspNetUserClaims" DROP CONSTRAINT IF EXISTS "PK_AspNetUserClaims";
ALTER TABLE IF EXISTS ONLY public."AspNetRoles" DROP CONSTRAINT IF EXISTS "PK_AspNetRoles";
ALTER TABLE IF EXISTS ONLY public."AspNetRoleClaims" DROP CONSTRAINT IF EXISTS "PK_AspNetRoleClaims";
DROP TABLE IF EXISTS public.whatsapp_sessions;
DROP TABLE IF EXISTS public.whatsapp_outbound_messages;
DROP TABLE IF EXISTS public.whatsapp_inbound_messages;
DROP TABLE IF EXISTS public.whatsapp_draft_attachments;
DROP TABLE IF EXISTS public.users;
DROP TABLE IF EXISTS public.units;
DROP TABLE IF EXISTS public.unit_memberships;
DROP TABLE IF EXISTS public.requests;
DROP TABLE IF EXISTS public.request_status_history;
DROP TABLE IF EXISTS public.request_messages;
DROP TABLE IF EXISTS public.request_attachments;
DROP TABLE IF EXISTS public.notifications;
DROP TABLE IF EXISTS public.management_company_request_categories;
DROP TABLE IF EXISTS public.management_company_employees;
DROP TABLE IF EXISTS public.management_companies;
DROP TABLE IF EXISTS public.condominiums;
DROP TABLE IF EXISTS public.condominium_memberships;
DROP TABLE IF EXISTS public.condominium_membership_roles;
DROP TABLE IF EXISTS public.condominium_blocks;
DROP TABLE IF EXISTS public.categories;
DROP TABLE IF EXISTS public."__EFMigrationsHistory";
DROP TABLE IF EXISTS public."AspNetUserTokens";
DROP TABLE IF EXISTS public."AspNetUserRoles";
DROP TABLE IF EXISTS public."AspNetUserLogins";
DROP TABLE IF EXISTS public."AspNetUserClaims";
DROP TABLE IF EXISTS public."AspNetRoles";
DROP TABLE IF EXISTS public."AspNetRoleClaims";
SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: AspNetRoleClaims; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetRoleClaims" (
    "Id" integer NOT NULL,
    "RoleId" uuid NOT NULL,
    "ClaimType" text,
    "ClaimValue" text
);


--
-- Name: AspNetRoleClaims_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."AspNetRoleClaims" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AspNetRoleClaims_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AspNetRoles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetRoles" (
    "Id" uuid NOT NULL,
    "Name" character varying(256),
    "NormalizedName" character varying(256),
    "ConcurrencyStamp" text
);


--
-- Name: AspNetUserClaims; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetUserClaims" (
    "Id" integer NOT NULL,
    "UserId" uuid NOT NULL,
    "ClaimType" text,
    "ClaimValue" text
);


--
-- Name: AspNetUserClaims_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."AspNetUserClaims" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AspNetUserClaims_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AspNetUserLogins; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetUserLogins" (
    "LoginProvider" text NOT NULL,
    "ProviderKey" text NOT NULL,
    "ProviderDisplayName" text,
    "UserId" uuid NOT NULL
);


--
-- Name: AspNetUserRoles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetUserRoles" (
    "UserId" uuid NOT NULL,
    "RoleId" uuid NOT NULL
);


--
-- Name: AspNetUserTokens; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetUserTokens" (
    "UserId" uuid NOT NULL,
    "LoginProvider" text NOT NULL,
    "Name" text NOT NULL,
    "Value" text
);


--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


--
-- Name: categories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.categories (
    id uuid NOT NULL,
    condominium_id uuid NOT NULL,
    name character varying(100) NOT NULL,
    normalized_name character varying(100) NOT NULL,
    description character varying(500),
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);


--
-- Name: condominium_blocks; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.condominium_blocks (
    id uuid NOT NULL,
    condominium_id uuid NOT NULL,
    identifier character varying(50) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);


--
-- Name: condominium_membership_roles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.condominium_membership_roles (
    id uuid NOT NULL,
    condominium_membership_id uuid NOT NULL,
    role integer NOT NULL,
    is_active boolean NOT NULL,
    granted_at timestamp with time zone NOT NULL,
    revoked_at timestamp with time zone
);


--
-- Name: condominium_memberships; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.condominium_memberships (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    condominium_id uuid NOT NULL,
    is_active boolean NOT NULL,
    joined_at timestamp with time zone NOT NULL,
    ended_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL
);


--
-- Name: condominiums; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.condominiums (
    id uuid NOT NULL,
    name character varying(200) NOT NULL,
    email character varying(254),
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    management_company_id uuid,
    address character varying(200),
    city character varying(100),
    cnpj character varying(14),
    doorman_contact character varying(100),
    has_doorman boolean DEFAULT false NOT NULL,
    is_remote_doorman boolean DEFAULT false NOT NULL,
    state character varying(2),
    whatsapp_display_name character varying(200),
    whatsapp_updates_enabled boolean DEFAULT false NOT NULL
);


--
-- Name: management_companies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.management_companies (
    id uuid NOT NULL,
    name character varying(150) NOT NULL,
    cnpj character varying(20),
    email character varying(254),
    phone_number character varying(30),
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    address character varying(200),
    city character varying(100),
    state character varying(2)
);


--
-- Name: management_company_employees; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.management_company_employees (
    id uuid NOT NULL,
    management_company_id uuid NOT NULL,
    user_id uuid NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    job_title character varying(100) DEFAULT 'Não informado'::character varying NOT NULL
);


--
-- Name: management_company_request_categories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.management_company_request_categories (
    id uuid NOT NULL,
    management_company_id uuid NOT NULL,
    name character varying(150) NOT NULL,
    normalized_name character varying(150) NOT NULL,
    description character varying(500),
    form_type character varying(50) NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);


--
-- Name: notifications; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.notifications (
    id uuid NOT NULL,
    recipient_user_id uuid NOT NULL,
    condominium_id uuid NOT NULL,
    type integer NOT NULL,
    title character varying(160) NOT NULL,
    body character varying(500) NOT NULL,
    request_id uuid,
    created_at timestamp with time zone NOT NULL,
    read_at timestamp with time zone
);


--
-- Name: request_attachments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.request_attachments (
    id uuid NOT NULL,
    request_id uuid NOT NULL,
    request_message_id uuid,
    uploaded_by_user_id uuid NOT NULL,
    original_file_name character varying(255) NOT NULL,
    storage_key character varying(500) NOT NULL,
    content_type character varying(100) NOT NULL,
    file_size bigint NOT NULL,
    created_at timestamp with time zone NOT NULL
);


--
-- Name: request_messages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.request_messages (
    id uuid NOT NULL,
    request_id uuid NOT NULL,
    author_user_id uuid NOT NULL,
    content character varying(4000) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    channel integer DEFAULT 1 NOT NULL
);


--
-- Name: request_status_history; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.request_status_history (
    id uuid NOT NULL,
    request_id uuid NOT NULL,
    previous_status integer,
    new_status integer NOT NULL,
    changed_by_user_id uuid NOT NULL,
    reason character varying(500),
    created_at timestamp with time zone NOT NULL
);


--
-- Name: requests; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.requests (
    id uuid NOT NULL,
    condominium_id uuid NOT NULL,
    author_user_id uuid NOT NULL,
    target_unit_id uuid,
    category_id uuid NOT NULL,
    title character varying(200) NOT NULL,
    description character varying(4000) NOT NULL,
    status integer NOT NULL,
    priority integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    resolved_at timestamp with time zone,
    source integer DEFAULT 1 NOT NULL
);


--
-- Name: unit_memberships; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.unit_memberships (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    unit_id uuid NOT NULL,
    relationship_type integer NOT NULL,
    is_resident boolean NOT NULL,
    is_primary_residence boolean NOT NULL,
    is_active boolean NOT NULL,
    started_at timestamp with time zone NOT NULL,
    ended_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL
);


--
-- Name: units; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.units (
    id uuid NOT NULL,
    condominium_id uuid NOT NULL,
    identifier character varying(50) NOT NULL,
    floor character varying(20),
    description character varying(500),
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    block_id uuid
);


--
-- Name: users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.users (
    id uuid NOT NULL,
    full_name character varying(200) NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    user_name character varying(254) NOT NULL,
    normalized_user_name character varying(254) NOT NULL,
    email character varying(254) NOT NULL,
    normalized_email character varying(254) NOT NULL,
    email_confirmed boolean NOT NULL,
    password_hash text,
    security_stamp text,
    concurrency_stamp text,
    phone_number character varying(30),
    phone_number_confirmed boolean NOT NULL,
    two_factor_enabled boolean NOT NULL,
    lockout_end timestamp with time zone,
    lockout_enabled boolean NOT NULL,
    access_failed_count integer NOT NULL,
    active_management_condominium_id uuid,
    uses_consolidated_management_scope boolean,
    address character varying(200),
    city character varying(100),
    cnpj character varying(14),
    cpf character varying(11),
    state character varying(2),
    last_login_at timestamp with time zone,
    must_change_password boolean DEFAULT false NOT NULL,
    password_changed_at timestamp with time zone,
    receive_whatsapp_updates boolean DEFAULT false NOT NULL
);


--
-- Name: whatsapp_draft_attachments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.whatsapp_draft_attachments (
    id uuid NOT NULL,
    session_id uuid NOT NULL,
    external_media_id character varying(200) NOT NULL,
    original_file_name character varying(255) NOT NULL,
    storage_key character varying(500) NOT NULL,
    content_type character varying(100) NOT NULL,
    file_size bigint NOT NULL,
    created_at timestamp with time zone NOT NULL
);


--
-- Name: whatsapp_inbound_messages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.whatsapp_inbound_messages (
    id uuid NOT NULL,
    external_message_id character varying(200) NOT NULL,
    phone_number character varying(20) NOT NULL,
    message_type character varying(40) NOT NULL,
    text character varying(4000),
    provider_timestamp timestamp with time zone NOT NULL,
    received_at timestamp with time zone NOT NULL,
    processed_at timestamp with time zone,
    identified_user_id uuid,
    processing_result character varying(100)
);


--
-- Name: whatsapp_outbound_messages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.whatsapp_outbound_messages (
    id uuid NOT NULL,
    request_id uuid NOT NULL,
    request_message_id uuid,
    user_id uuid NOT NULL,
    condominium_id uuid NOT NULL,
    destination_phone character varying(20) NOT NULL,
    notification_type integer NOT NULL,
    send_mode integer NOT NULL,
    template_name character varying(200),
    template_language character varying(20),
    content character varying(1000) NOT NULL,
    external_message_id character varying(200),
    status integer NOT NULL,
    attempt_count integer NOT NULL,
    manual_retry_count integer NOT NULL,
    next_attempt_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    sent_at timestamp with time zone,
    delivered_at timestamp with time zone,
    read_at timestamp with time zone,
    failed_at timestamp with time zone,
    last_error_code character varying(100),
    last_error_description character varying(500),
    idempotency_key character varying(250) NOT NULL,
    version uuid NOT NULL
);


--
-- Name: whatsapp_sessions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.whatsapp_sessions (
    id uuid NOT NULL,
    phone_number character varying(20) NOT NULL,
    user_id uuid,
    condominium_id uuid,
    unit_id uuid,
    request_id uuid,
    state integer NOT NULL,
    previous_state integer,
    last_interaction_at timestamp with time zone NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    version uuid NOT NULL,
    category_id uuid,
    draft_description character varying(4000),
    page integer DEFAULT 0 NOT NULL
);


--
-- Data for Name: AspNetRoleClaims; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AspNetRoleClaims" ("Id", "RoleId", "ClaimType", "ClaimValue") FROM stdin;
\.


--
-- Data for Name: AspNetRoles; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") FROM stdin;
019f7ceb-deae-7abc-b5ea-5759f7c2ce3e	PlatformAdmin	PLATFORMADMIN	702c3e8f-e0c1-4135-bc98-85ec630b25f3
019f8039-1eb0-7160-b02e-19b7273c5a01	Manager	MANAGER	20963755-290e-41fd-9096-0bec497d63e7
\.


--
-- Data for Name: AspNetUserClaims; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AspNetUserClaims" ("Id", "UserId", "ClaimType", "ClaimValue") FROM stdin;
\.


--
-- Data for Name: AspNetUserLogins; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AspNetUserLogins" ("LoginProvider", "ProviderKey", "ProviderDisplayName", "UserId") FROM stdin;
\.


--
-- Data for Name: AspNetUserRoles; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AspNetUserRoles" ("UserId", "RoleId") FROM stdin;
c63718e2-d2a6-4822-ac99-2e05d0912be4	019f7ceb-deae-7abc-b5ea-5759f7c2ce3e
4e48eb75-23a3-458c-b1c7-ab9a44a5e786	019f8039-1eb0-7160-b02e-19b7273c5a01
ae947f34-226d-45d3-8dd8-3778b237d5bf	019f8039-1eb0-7160-b02e-19b7273c5a01
7bcbd08e-4607-4cd0-a9b6-d492002c65a0	019f8039-1eb0-7160-b02e-19b7273c5a01
\.


--
-- Data for Name: AspNetUserTokens; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AspNetUserTokens" ("UserId", "LoginProvider", "Name", "Value") FROM stdin;
\.


--
-- Data for Name: __EFMigrationsHistory; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;
20260716115958_InitialCondominium	10.0.4
20260716122411_AddUnits	10.0.4
20260716124441_AddIdentityUsers	10.0.4
20260716131045_AddCondominiumMemberships	10.0.4
20260716132204_AddCondominiumMembershipRoles	10.0.4
20260716134741_AddUnitMemberships	10.0.4
20260716140239_AddCategories	10.0.4
20260716141737_AddRequests	10.0.4
20260716161356_AddRequestMessages	10.0.4
20260716190819_AddRequestAttachments	10.0.4
20260717014602_AddCondominiumBlocks	10.0.4
20260717131827_AddManagementContext	10.0.4
20260717160052_AddActiveManagementCondominiumId	10.0.4
20260726002634_AddManagementCompanies	10.0.4
20260726003752_AddManagementCompanyToCondominiums	10.0.4
20260726005042_AddManagementCompanyEmployees	10.0.4
20260726013748_AddManagementCompanyRequestCategories	10.0.4
20260727130346_ExpandRegistrationLot2	10.0.4
20260727144132_AddNotifications	10.0.4
20260728022625_AddUserPasswordLifecycle	10.0.4
20260728130542_AddWhatsAppFoundation	10.0.4
20260728132848_ExpandWhatsAppRequestFlow	10.0.4
20260728140545_AddWhatsAppOutboundNotifications	10.0.4
\.


--
-- Data for Name: categories; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.categories (id, condominium_id, name, normalized_name, description, is_active, created_at, updated_at) FROM stdin;
45e58a41-be3f-45d5-907d-ab5dc8a1952d	e61d21e3-8cab-47e3-bd28-341a78a457a9	acessibilidade	ACESSIBILIDADE	\N	t	2026-07-16 19:30:29.024968+00	2026-07-16 19:30:29.024968+00
4954855b-3b86-4a53-b725-442aa68d47b4	e61d21e3-8cab-47e3-bd28-341a78a457a9	Amanhã	AMANHÃ	\N	t	2026-07-17 02:56:41.695602+00	2026-07-17 02:56:41.695602+00
60011118-f2f0-4135-b39a-76f963bbed0b	e61d21e3-8cab-47e3-bd28-341a78a457a9	Manutençã	MANUTENÇÃ	Problemas e reparos	t	2026-07-16 14:04:13.184194+00	2026-07-17 02:58:32.692749+00
add6212f-fc70-4214-9628-483a102a37b6	e61d21e3-8cab-47e3-bd28-341a78a457a9	Encomenda	ENCOMENDA	Problemas com entrega de encomendas	t	2026-07-16 19:37:33.671988+00	2026-07-17 03:09:34.143787+00
016320f0-869e-48ea-8b6e-cdd4194bb51c	e61d21e3-8cab-47e3-bd28-341a78a457a9	Barulho	BARULHO	\N	t	2026-07-16 14:04:13.287767+00	2026-07-17 03:10:04.692966+00
1dcfb512-e79c-4eda-b2a6-38a97081ee8a	57f10b5c-f01b-401e-af00-879611ac61c3	Teste	TESTE	\N	t	2026-07-20 17:28:04.9798+00	2026-07-20 17:28:04.9798+00
87145d32-e18f-406c-b29c-50fdb5c99675	57f10b5c-f01b-401e-af00-879611ac61c3	Teste 2	TESTE 2	\N	t	2026-07-20 17:28:08.189128+00	2026-07-20 17:28:08.189128+00
e7fa5ae9-611b-48f1-b0a9-13f262f845cc	ba468e96-7cb3-4150-8a1d-d4530f212edf	teste imnga	TESTE IMNGA	\N	t	2026-07-20 17:58:25.088029+00	2026-07-20 17:58:25.088029+00
dfcbdf64-ecc9-4279-8996-3d3e3c039f13	59d9844b-2207-4884-ac68-43357392b2c3	Manutenção	MANUTENÇÃO	\N	t	2026-07-27 14:48:19.261269+00	2026-07-27 14:48:19.261269+00
\.


--
-- Data for Name: condominium_blocks; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.condominium_blocks (id, condominium_id, identifier, created_at, updated_at) FROM stdin;
13ce7422-5017-494f-bed7-cd7fc4bd51f2	e61d21e3-8cab-47e3-bd28-341a78a457a9	1	2026-07-17 01:53:32.352934+00	2026-07-17 01:53:32.352934+00
9233bd9d-ea96-46d2-af69-636ed7c827c9	e61d21e3-8cab-47e3-bd28-341a78a457a9	2	2026-07-17 01:53:32.352934+00	2026-07-17 02:01:04.427233+00
ce8429ae-06b3-4ee8-883c-3af23ffed690	e61d21e3-8cab-47e3-bd28-341a78a457a9	4	2026-07-17 01:53:32.352934+00	2026-07-17 02:01:08.60587+00
4ecea3c5-e53b-4b57-b9fc-5778a6332272	e61d21e3-8cab-47e3-bd28-341a78a457a9	3	2026-07-17 01:53:32.352934+00	2026-07-17 02:16:10.053081+00
0a1dcf4f-d3a8-49ca-a725-2364b80c53dc	ba468e96-7cb3-4150-8a1d-d4530f212edf	1	2026-07-20 16:40:07.418381+00	2026-07-20 16:40:15.697905+00
53d8d657-78f8-4641-9ff7-99f200cb45ec	57f10b5c-f01b-401e-af00-879611ac61c3	1	2026-07-20 16:40:57.099045+00	2026-07-20 16:40:57.099045+00
d190e2aa-86f3-4f14-aa68-abb8651ea49c	ba468e96-7cb3-4150-8a1d-d4530f212edf	2	2026-07-20 16:43:14.86335+00	2026-07-20 16:43:14.86335+00
c07f8a4c-6f3e-49a0-9c5d-33a114929cba	57f10b5c-f01b-401e-af00-879611ac61c3	2	2026-07-20 17:55:51.143161+00	2026-07-20 17:55:51.143161+00
669a3da0-432b-49e4-9103-09353a1caf48	59d9844b-2207-4884-ac68-43357392b2c3	1	2026-07-27 12:24:55.864471+00	2026-07-27 12:24:55.864471+00
848971b3-57b5-4f9f-a30d-910e315a8e7b	e61d21e3-8cab-47e3-bd28-341a78a457a9	6	2026-07-28 03:48:52.993818+00	2026-07-28 03:48:52.993818+00
f63b2806-634c-41a3-89db-43ffab39e38e	e61d21e3-8cab-47e3-bd28-341a78a457a9	5	2026-07-28 03:48:52.982676+00	2026-07-28 03:48:52.982676+00
\.


--
-- Data for Name: condominium_membership_roles; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.condominium_membership_roles (id, condominium_membership_id, role, is_active, granted_at, revoked_at) FROM stdin;
af74b35a-1090-45a2-8fa8-84da2de8f611	cf92f1c7-d668-4742-94e1-bcec799add50	2	t	2026-07-16 13:23:08.055608+00	\N
fb2cc20c-303e-4dc6-91ba-6694dc02e7d3	582014d6-cb62-4733-a3c3-5fc43d9a7201	2	t	2026-07-16 18:06:06.185471+00	\N
fb805520-c641-46a3-85e7-dbc75cc610e3	0671a4e9-6c26-4cba-9ab7-add1e8b9f969	1	t	2026-07-16 18:39:30.811027+00	\N
1b01d3e9-eebc-43ce-9479-aa650981ea3e	0671a4e9-6c26-4cba-9ab7-add1e8b9f969	2	t	2026-07-16 18:39:30.991234+00	\N
81a96b1e-9885-4061-8fbf-21ab98a51d30	0f260892-bc85-46b6-8922-626e3118570f	2	t	2026-07-16 19:43:55.645533+00	\N
dc76321f-9124-4162-909a-8a1a10f25b86	6dd74d7f-247d-45ab-a74f-5023942d865c	2	t	2026-07-16 20:47:55.386059+00	\N
060ad252-d2df-4629-96de-6e5da095c1f0	a5bf0ee7-ea7f-486d-9046-e93f2a514f71	1	t	2026-07-20 00:03:29.78508+00	\N
8c273504-06ac-46e2-a14c-f2f5723c18a4	0353c16b-34b4-41ca-861c-5f8a92a807e8	2	t	2026-07-20 16:39:57.486528+00	\N
3f249aeb-aa4b-43dd-bdf7-b5dfc505c431	e7f09942-4ec2-4bc6-a3eb-b0fc2cae293d	2	t	2026-07-20 16:41:32.002499+00	\N
72a4daa0-1757-480c-8072-19d5e7ba3545	a750c4c4-0acc-4e79-8dbf-7be47cb324b0	2	t	2026-07-20 17:30:18.575027+00	\N
64666c19-ad2e-42de-949e-8d67cf381b1a	8c7cb56a-9035-40e6-b96a-80718f90e7a5	2	t	2026-07-20 17:30:53.670056+00	\N
1e4b16bb-4cc6-49f6-be97-3464398f5ca4	1dd34dfa-35b8-433e-a10b-0f9928aa2c6e	2	t	2026-07-20 17:31:10.796831+00	\N
93ae6d24-ae2e-40e5-ac96-20de17104f2e	e7c8d1ae-a653-4d7f-9298-bd1f63c20bc4	2	t	2026-07-20 17:55:18.47342+00	\N
d5697dd8-36dc-4129-8b98-10373938d606	28a6411f-81cf-4a20-8446-7068a22946c4	2	t	2026-07-20 17:56:18.530917+00	\N
ce33c7ce-3000-4c81-ac21-3e29ccd2405e	81ab8610-0f7b-4c22-85fc-f35c30c9947f	2	t	2026-07-20 17:57:44.114741+00	\N
a09487ad-6302-4e1f-8095-3386316d3e26	a7318873-7d60-46c1-bed5-2bbfd7b991f8	1	f	2026-07-20 16:38:24.414587+00	2026-07-27 12:18:18.388244+00
108123c4-a97f-4f57-a283-57980d2b981f	23b8f34b-5628-4c49-b453-6d423ead35e9	1	t	2026-07-27 12:18:24.182593+00	\N
29885151-b1da-4ecd-b73e-d36c9083f683	0b8a3a5d-047f-41f7-98e7-9b783c7ca98b	2	t	2026-07-27 12:25:33.432201+00	\N
9f746e48-ab41-439e-855c-30564933c100	b028890d-ae63-4df3-b571-663131fa6252	1	f	2026-07-27 13:17:55.98165+00	2026-07-27 13:17:58.956311+00
bcec336c-ed32-4f2e-a818-1ab36e705831	cf92f1c7-d668-4742-94e1-bcec799add50	1	f	2026-07-16 13:23:07.934577+00	2026-07-27 13:18:03.149876+00
b10eb4a0-e08f-475f-b725-4925b9dd61bf	13eb29d9-db42-45bf-9674-ea52c8929570	1	t	2026-07-27 13:54:27.358453+00	\N
62b54097-9c4a-40fc-ae9a-b1763e57d4aa	16136a70-b327-47f8-8e6a-0af679592d9c	1	f	2026-07-20 16:29:19.675747+00	2026-07-27 13:56:36.242703+00
a8cb49ca-3980-4359-85bc-5697424e8b46	a91a371f-8329-441e-8d29-94dd3e4eece3	1	f	2026-07-27 13:56:36.24223+00	2026-07-27 13:57:15.084289+00
d9d65b91-a160-401d-a4cf-92cfe8a4bd91	568a8627-f0f1-4517-aa47-a8b4923c18f5	1	t	2026-07-27 14:00:30.371731+00	\N
c8346170-a099-4fb0-97a4-77d7b2c597f5	314f102f-3d36-4019-acc5-a0a85b631c0c	1	f	2026-07-17 14:11:33.592414+00	2026-07-27 14:00:41.889319+00
81c35437-19b4-448b-b5c3-2750aa15093a	b436efcd-8d13-4884-9466-e0c0e4ac5113	1	f	2026-07-27 14:00:41.889258+00	2026-07-27 15:58:30.957509+00
c415dc0e-d3f1-472e-96b4-e40ecab6f38e	e926104b-d562-4032-b17b-91ffe5f6257c	1	t	2026-07-27 15:58:30.954917+00	\N
eb2cf78d-a306-4f6c-9417-3b527cadb410	da1ef96e-eb4e-4cca-921e-5e3fb5fa6ee5	2	t	2026-07-28 02:47:43.62578+00	\N
\.


--
-- Data for Name: condominium_memberships; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.condominium_memberships (id, user_id, condominium_id, is_active, joined_at, ended_at, created_at) FROM stdin;
cf92f1c7-d668-4742-94e1-bcec799add50	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	e61d21e3-8cab-47e3-bd28-341a78a457a9	t	2026-07-16 13:12:54.422887+00	\N	2026-07-16 13:12:54.422887+00
0671a4e9-6c26-4cba-9ab7-add1e8b9f969	c63718e2-d2a6-4822-ac99-2e05d0912be4	e61d21e3-8cab-47e3-bd28-341a78a457a9	t	2026-07-16 17:51:56.788752+00	\N	2026-07-16 17:51:56.788752+00
582014d6-cb62-4733-a3c3-5fc43d9a7201	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	e61d21e3-8cab-47e3-bd28-341a78a457a9	t	2026-07-16 18:06:06.148089+00	\N	2026-07-16 18:06:06.148089+00
0f260892-bc85-46b6-8922-626e3118570f	c08f02ee-2a03-4214-94a5-c5956fbe0dff	e61d21e3-8cab-47e3-bd28-341a78a457a9	t	2026-07-16 19:43:55.645022+00	\N	2026-07-16 19:43:55.645022+00
6dd74d7f-247d-45ab-a74f-5023942d865c	10a2a96c-8f5f-4a05-a750-6a5a928aebea	e61d21e3-8cab-47e3-bd28-341a78a457a9	t	2026-07-16 20:47:55.385566+00	\N	2026-07-16 20:47:55.385566+00
314f102f-3d36-4019-acc5-a0a85b631c0c	4e373240-3166-42a4-89a1-2e2ef41ec63c	d721c30f-1417-465e-91fc-930eb53a2cd8	t	2026-07-17 14:11:26.499688+00	\N	2026-07-17 14:11:26.499688+00
a5bf0ee7-ea7f-486d-9046-e93f2a514f71	c63718e2-d2a6-4822-ac99-2e05d0912be4	089690fa-6073-43a3-b317-6962679905ae	t	2026-07-20 00:03:29.784683+00	\N	2026-07-20 00:03:29.784683+00
16136a70-b327-47f8-8e6a-0af679592d9c	4e48eb75-23a3-458c-b1c7-ab9a44a5e786	ba468e96-7cb3-4150-8a1d-d4530f212edf	t	2026-07-20 16:29:19.648721+00	\N	2026-07-20 16:29:19.648721+00
a7318873-7d60-46c1-bed5-2bbfd7b991f8	4e48eb75-23a3-458c-b1c7-ab9a44a5e786	57f10b5c-f01b-401e-af00-879611ac61c3	t	2026-07-20 16:38:24.374217+00	\N	2026-07-20 16:38:24.374217+00
0353c16b-34b4-41ca-861c-5f8a92a807e8	d2b48f78-26ff-452b-b864-8eedbf5793b2	ba468e96-7cb3-4150-8a1d-d4530f212edf	t	2026-07-20 16:39:57.486519+00	\N	2026-07-20 16:39:57.486519+00
e7f09942-4ec2-4bc6-a3eb-b0fc2cae293d	7fbf6731-8774-4bc8-acfe-b386a54a6bf8	57f10b5c-f01b-401e-af00-879611ac61c3	t	2026-07-20 16:41:32.002491+00	\N	2026-07-20 16:41:32.002491+00
a750c4c4-0acc-4e79-8dbf-7be47cb324b0	8e3ad79b-fba4-462e-a913-9cd31cfdfe1f	ba468e96-7cb3-4150-8a1d-d4530f212edf	t	2026-07-20 17:30:18.574554+00	\N	2026-07-20 17:30:18.574554+00
8c7cb56a-9035-40e6-b96a-80718f90e7a5	1bfdc06d-9f08-400e-a3da-a227d747a5fa	ba468e96-7cb3-4150-8a1d-d4530f212edf	t	2026-07-20 17:30:53.670049+00	\N	2026-07-20 17:30:53.670049+00
1dd34dfa-35b8-433e-a10b-0f9928aa2c6e	1e80376a-c6b4-445b-8a6f-0ced2d85f8c2	57f10b5c-f01b-401e-af00-879611ac61c3	t	2026-07-20 17:31:10.796822+00	\N	2026-07-20 17:31:10.796822+00
e7c8d1ae-a653-4d7f-9298-bd1f63c20bc4	3526eabc-0c28-40f4-8ae4-24b1fcdcae51	57f10b5c-f01b-401e-af00-879611ac61c3	t	2026-07-20 17:55:18.456864+00	\N	2026-07-20 17:55:18.456864+00
28a6411f-81cf-4a20-8446-7068a22946c4	368e5e05-df65-4a38-8c74-f29086b6029a	57f10b5c-f01b-401e-af00-879611ac61c3	t	2026-07-20 17:56:18.53014+00	\N	2026-07-20 17:56:18.53014+00
81ab8610-0f7b-4c22-85fc-f35c30c9947f	9fc09869-4ffd-4d49-83c6-2aff3d89e2a5	ba468e96-7cb3-4150-8a1d-d4530f212edf	t	2026-07-20 17:57:44.114197+00	\N	2026-07-20 17:57:44.114197+00
23b8f34b-5628-4c49-b453-6d423ead35e9	ae947f34-226d-45d3-8dd8-3778b237d5bf	57f10b5c-f01b-401e-af00-879611ac61c3	t	2026-07-26 04:10:31.727354+00	\N	2026-07-26 04:10:31.727354+00
568a8627-f0f1-4517-aa47-a8b4923c18f5	7bcbd08e-4607-4cd0-a9b6-d492002c65a0	59d9844b-2207-4884-ac68-43357392b2c3	t	2026-07-27 12:24:16.768102+00	\N	2026-07-27 12:24:16.768102+00
0b8a3a5d-047f-41f7-98e7-9b783c7ca98b	2e38f0b9-18a0-446c-9ad7-009457fd86d6	59d9844b-2207-4884-ac68-43357392b2c3	t	2026-07-27 12:25:33.428253+00	\N	2026-07-27 12:25:33.428253+00
b028890d-ae63-4df3-b571-663131fa6252	ae947f34-226d-45d3-8dd8-3778b237d5bf	e61d21e3-8cab-47e3-bd28-341a78a457a9	t	2026-07-27 13:17:55.957675+00	\N	2026-07-27 13:17:55.957675+00
13eb29d9-db42-45bf-9674-ea52c8929570	ae947f34-226d-45d3-8dd8-3778b237d5bf	df45128b-d711-44bf-abd5-3d137f40853a	t	2026-07-27 13:54:27.332526+00	\N	2026-07-27 13:54:27.332526+00
a91a371f-8329-441e-8d29-94dd3e4eece3	ae947f34-226d-45d3-8dd8-3778b237d5bf	ba468e96-7cb3-4150-8a1d-d4530f212edf	t	2026-07-27 13:56:36.235826+00	\N	2026-07-27 13:56:36.235826+00
b436efcd-8d13-4884-9466-e0c0e4ac5113	7bcbd08e-4607-4cd0-a9b6-d492002c65a0	d721c30f-1417-465e-91fc-930eb53a2cd8	t	2026-07-27 14:00:41.888646+00	\N	2026-07-27 14:00:41.888646+00
e926104b-d562-4032-b17b-91ffe5f6257c	4e48eb75-23a3-458c-b1c7-ab9a44a5e786	d721c30f-1417-465e-91fc-930eb53a2cd8	t	2026-07-27 15:58:30.93538+00	\N	2026-07-27 15:58:30.93538+00
da1ef96e-eb4e-4cca-921e-5e3fb5fa6ee5	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	t	2026-07-28 02:47:43.607958+00	\N	2026-07-28 02:47:43.607958+00
\.


--
-- Data for Name: condominiums; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.condominiums (id, name, email, is_active, created_at, updated_at, management_company_id, address, city, cnpj, doorman_contact, has_doorman, is_remote_doorman, state, whatsapp_display_name, whatsapp_updates_enabled) FROM stdin;
e61d21e3-8cab-47e3-bd28-341a78a457a9	Condomínio Monticello	contato@monticello.com.br	t	2026-07-16 12:01:49.909417+00	2026-07-16 12:01:49.909417+00	\N	\N	\N	\N	\N	f	f	\N	\N	f
d721c30f-1417-465e-91fc-930eb53a2cd8	Test Condo	\N	t	2026-07-17 14:10:24.866752+00	2026-07-17 14:10:24.866752+00	\N	\N	\N	\N	\N	f	f	\N	\N	f
df45128b-d711-44bf-abd5-3d137f40853a	Real Village	realvillage@email.com	t	2026-07-19 23:52:25.428375+00	2026-07-26 03:49:47.430261+00	becfc87e-1efe-4e3b-a605-6577c0adb4ee	\N	\N	\N	\N	f	f	\N	\N	f
57f10b5c-f01b-401e-af00-879611ac61c3	Condomínio Central	\N	t	2026-07-16 12:01:50.661259+00	2026-07-27 13:17:30.204157+00	becfc87e-1efe-4e3b-a605-6577c0adb4ee	Centro	maringa	55445207000161	4499665656	t	t	PR	\N	f
089690fa-6073-43a3-b317-6962679905ae	Royal Village	string	t	2026-07-20 00:03:29.784325+00	2026-07-27 13:17:39.826765+00	\N	\N	\N	\N	\N	f	f	\N	\N	f
ba468e96-7cb3-4150-8a1d-d4530f212edf	Maria do Ingá	maria@doinga.com	t	2026-07-20 16:28:56.943841+00	2026-07-27 13:55:27.072426+00	5c07ebf8-4ca4-4a48-aa74-78f98acd029a	Av. Monteiro Lobato, 1530	Maringá	10453391000153	\N	f	f	MG	\N	f
59d9844b-2207-4884-ac68-43357392b2c3	Spazio Mendonza	spazio@mendonza.com	t	2026-07-20 14:53:59.551134+00	2026-07-27 14:50:51.321705+00	becfc87e-1efe-4e3b-a605-6577c0adb4ee	\N	\N	\N	\N	f	f	\N	\N	f
\.


--
-- Data for Name: management_companies; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.management_companies (id, name, cnpj, email, phone_number, is_active, created_at, updated_at, address, city, state) FROM stdin;
becfc87e-1efe-4e3b-a605-6577c0adb4ee	Dimarp	CNPJ654654	condominios@dimarp.com	44997562161	t	2026-07-26 03:28:09.986759+00	2026-07-26 03:28:09.986759+00	\N	\N	\N
5c07ebf8-4ca4-4a48-aa74-78f98acd029a	Resolv	546543219864	resolv@resolv.com	44666666666	t	2026-07-27 11:57:49.143496+00	2026-07-27 11:57:49.143496+00	\N	\N	\N
\.


--
-- Data for Name: management_company_employees; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.management_company_employees (id, management_company_id, user_id, is_active, created_at, updated_at, job_title) FROM stdin;
2c0423d4-9e7b-43c7-a3e4-f75db778ef9c	becfc87e-1efe-4e3b-a605-6577c0adb4ee	c1ba77a2-b36c-4987-9ae7-6b7e8b9bcd70	t	2026-07-26 03:28:34.151837+00	2026-07-26 03:28:52.735319+00	Não informado
ce80edf3-a0ea-4f09-84b3-7a80aa01d78b	becfc87e-1efe-4e3b-a605-6577c0adb4ee	ee187bc0-c4ba-4a99-bb1b-f25551c179ed	t	2026-07-26 04:09:33.2252+00	2026-07-27 12:21:24.103802+00	Não informado
\.


--
-- Data for Name: management_company_request_categories; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.management_company_request_categories (id, management_company_id, name, normalized_name, description, form_type, is_active, created_at, updated_at) FROM stdin;
\.


--
-- Data for Name: notifications; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.notifications (id, recipient_user_id, condominium_id, type, title, body, request_id, created_at, read_at) FROM stdin;
299560b9-7dd7-47db-88c8-8ebbf0b94763	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	2	Status atualizado	Minha encomenda sumiu: Aberta → Em andamento	e8be5415-6203-4b08-a6c2-fa52bae5fe26	2026-07-28 03:53:38.212164+00	\N
bf90232c-e30b-4734-8812-f84a0045a117	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	2	Status atualizado	Minha encomenda sumiu: Em andamento → Aguardando terceiro	e8be5415-6203-4b08-a6c2-fa52bae5fe26	2026-07-28 03:53:53.665757+00	\N
1362dd57-ac71-418c-8a65-68fb34f89db1	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	4	Nova mensagem	Minha encomenda sumiu: Espera ai kct	e8be5415-6203-4b08-a6c2-fa52bae5fe26	2026-07-28 03:54:03.549909+00	\N
812ed9af-1e30-4586-99f3-27c4d11f5bac	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	2	Status atualizado	Minha encomenda sumiu: Aguardando terceiro → Resolvida	e8be5415-6203-4b08-a6c2-fa52bae5fe26	2026-07-28 03:54:30.002142+00	\N
b347cb7f-43d3-4bd1-96db-3d2e38f93e14	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	2	Status atualizado	Nao congiso dormir: Aberta → Resolvida	cf8d4ef1-741c-45a7-91ea-2d0a73e7ed18	2026-07-28 03:57:00.736103+00	\N
3a47d279-ab57-4b81-83e1-277261982cfd	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	2	Status atualizado	werwe: Aberta → Cancelada	79669645-30c6-4dee-9f36-a07b0ac232a5	2026-07-28 03:59:17.069754+00	\N
0bf9fb7e-0459-46b7-b1e0-a015be6fbcab	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	2	Status atualizado	werwe: Cancelada → Aberta	79669645-30c6-4dee-9f36-a07b0ac232a5	2026-07-28 03:59:42.806927+00	\N
58137dc8-75f3-4652-9b1a-a9a5a1be616f	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	2	Status atualizado	werwe: Aberta → Cancelada	79669645-30c6-4dee-9f36-a07b0ac232a5	2026-07-28 03:59:59.527854+00	\N
4a887df4-86f9-45db-8f2c-b1431204b1ad	c63718e2-d2a6-4822-ac99-2e05d0912be4	e61d21e3-8cab-47e3-bd28-341a78a457a9	4	Nova mensagem	Minha encomenda sumiu: Sumiu	e8be5415-6203-4b08-a6c2-fa52bae5fe26	2026-07-28 03:53:13.891778+00	2026-07-28 04:03:51.187954+00
770b6730-2f0b-4235-9047-361144ba83f8	c63718e2-d2a6-4822-ac99-2e05d0912be4	e61d21e3-8cab-47e3-bd28-341a78a457a9	4	Nova mensagem	Nao congiso dormir: Pede pra parar o barulho lá seu bosta	cf8d4ef1-741c-45a7-91ea-2d0a73e7ed18	2026-07-28 03:56:04.955892+00	2026-07-28 04:03:51.187954+00
a145f3e3-7aa2-4650-91a7-65635c7356c4	c63718e2-d2a6-4822-ac99-2e05d0912be4	e61d21e3-8cab-47e3-bd28-341a78a457a9	1	Nova solicitação	Barulho: Nao congiso dormir	cf8d4ef1-741c-45a7-91ea-2d0a73e7ed18	2026-07-28 03:55:32.107005+00	2026-07-28 04:03:51.187954+00
b5200b0e-b899-4b2b-b867-102e7f5c2963	c63718e2-d2a6-4822-ac99-2e05d0912be4	e61d21e3-8cab-47e3-bd28-341a78a457a9	1	Nova solicitação	acessibilidade: werwe	79669645-30c6-4dee-9f36-a07b0ac232a5	2026-07-28 03:57:57.167664+00	2026-07-28 04:03:51.187954+00
d14599b9-1e8b-429b-bfa0-ab05ff9b082c	c63718e2-d2a6-4822-ac99-2e05d0912be4	e61d21e3-8cab-47e3-bd28-341a78a457a9	4	Nova mensagem	werwe: asdas	79669645-30c6-4dee-9f36-a07b0ac232a5	2026-07-28 03:58:01.056889+00	2026-07-28 04:03:51.187954+00
e97fe51c-9fbd-4c0b-b310-43971455af71	c63718e2-d2a6-4822-ac99-2e05d0912be4	e61d21e3-8cab-47e3-bd28-341a78a457a9	1	Nova solicitação	Encomenda: Minha encomenda sumiu	e8be5415-6203-4b08-a6c2-fa52bae5fe26	2026-07-28 03:53:04.746672+00	2026-07-28 04:03:51.187954+00
9b5c969e-2260-4aac-aad8-9ef1118d2e2d	c63718e2-d2a6-4822-ac99-2e05d0912be4	e61d21e3-8cab-47e3-bd28-341a78a457a9	1	Nova solicitação	Barulho: test	974b54e7-acf8-4189-b718-a1476a29b563	2026-07-28 04:48:49.9908+00	\N
ba309891-22bd-479f-819d-04b674630a8a	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	2	Status atualizado	test: Aberta → Resolvida	974b54e7-acf8-4189-b718-a1476a29b563	2026-07-28 04:49:10.851908+00	\N
1be5841b-3a1a-4eaf-bf8d-8a3b68187769	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	2	Status atualizado	test: Resolvida → Aberta	974b54e7-acf8-4189-b718-a1476a29b563	2026-07-28 04:49:25.945368+00	\N
9e3a3cf0-68ae-4524-add9-1675e69972bd	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	e61d21e3-8cab-47e3-bd28-341a78a457a9	2	Status atualizado	test: Aberta → Cancelada	974b54e7-acf8-4189-b718-a1476a29b563	2026-07-28 04:49:34.578969+00	\N
efabd148-7e85-4b4b-9eac-7328229c56ae	c63718e2-d2a6-4822-ac99-2e05d0912be4	e61d21e3-8cab-47e3-bd28-341a78a457a9	1	Nova solicitação	Amanhã: sdfas	6e273245-7079-4096-ba3b-028d61710b2f	2026-07-28 04:50:35.348719+00	\N
\.


--
-- Data for Name: request_attachments; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.request_attachments (id, request_id, request_message_id, uploaded_by_user_id, original_file_name, storage_key, content_type, file_size, created_at) FROM stdin;
8129369e-67b9-42e9-b2fe-19f00a7833eb	c9c4241f-c5a1-46e7-b289-16ec12496a64	\N	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	attachment-test.jpg	requests/c9c4241f-c5a1-46e7-b289-16ec12496a64/909740732eca442db1542fb2cd7a2636.jpg	image/jpeg	27	2026-07-16 19:10:50.235758+00
49a4cc5e-0971-4081-b5f7-72bfec471f45	c9c4241f-c5a1-46e7-b289-16ec12496a64	\N	c63718e2-d2a6-4822-ac99-2e05d0912be4	attachment-test.pdf	requests/c9c4241f-c5a1-46e7-b289-16ec12496a64/dbdacb78a7bc4ac4a284221b92e5d254.pdf	application/pdf	40	2026-07-16 19:11:05.035246+00
0457890d-b4dc-4ec9-8f82-aebd58f6d0ae	c9c4241f-c5a1-46e7-b289-16ec12496a64	\N	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	attachment-test.pdf	requests/c9c4241f-c5a1-46e7-b289-16ec12496a64/b324b18da468493ca3aec01e6cf41513.pdf	application/pdf	40	2026-07-16 19:11:33.853118+00
3aa82936-40d4-4adb-8838-ecaca4e52750	c9c4241f-c5a1-46e7-b289-16ec12496a64	\N	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	attachment-test.jpg	requests/c9c4241f-c5a1-46e7-b289-16ec12496a64/f74931a2eb3d47c2b007e34e74b29837.jpg	image/jpeg	27	2026-07-16 19:11:33.852933+00
ba5f7890-7c95-40d8-8dca-4a2b5a74def9	c9c4241f-c5a1-46e7-b289-16ec12496a64	\N	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	attachment-test.jpg	requests/c9c4241f-c5a1-46e7-b289-16ec12496a64/80044ab5a6b7455fb71e101f64245a5c.jpg	image/jpeg	27	2026-07-16 19:13:46.401261+00
bfea3510-c205-4154-85df-2194494c3f11	2b71b542-22ef-4a10-b60f-9ac469e4ca0c	\N	c63718e2-d2a6-4822-ac99-2e05d0912be4	CTPS.pdf	requests/2b71b542-22ef-4a10-b60f-9ac469e4ca0c/2c5462f7dcab4a298fbe7d6171b962f5.pdf	application/pdf	176645	2026-07-27 19:51:06.808576+00
7f8bfdf0-beaa-4dfe-a50f-387d893b6f22	cf8d4ef1-741c-45a7-91ea-2d0a73e7ed18	\N	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	3X4.jpeg	requests/cf8d4ef1-741c-45a7-91ea-2d0a73e7ed18/d4f9552731034c8ebd2058d615588abe.jpeg	image/jpeg	66581	2026-07-28 03:55:39.635771+00
c752e139-a404-4911-848f-c6011806665c	cf8d4ef1-741c-45a7-91ea-2d0a73e7ed18	\N	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	ft1.jpg	requests/cf8d4ef1-741c-45a7-91ea-2d0a73e7ed18/8c9b1d5667af403fbeaf822566d07f74.jpg	image/jpeg	1064011	2026-07-28 03:55:48.435612+00
d100382d-8260-490b-9b0b-b8d079dbba6d	974b54e7-acf8-4189-b718-a1476a29b563	\N	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	3X4 (1).jpeg	requests/974b54e7-acf8-4189-b718-a1476a29b563/df8ffdddc56e41209a481b5e6a246908.jpeg	image/jpeg	66581	2026-07-28 04:48:56.326075+00
\.


--
-- Data for Name: request_messages; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.request_messages (id, request_id, author_user_id, content, created_at, channel) FROM stdin;
c9dbc6fb-6ac2-4141-8bb5-aeca81876677	d47ec655-3284-40bf-aac7-d95c65e32f89	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	Gostaria de uma atualização.	2026-07-16 16:14:50.886375+00	1
781111f2-3a9c-4e62-a30f-3203e28cc5d3	d47ec655-3284-40bf-aac7-d95c65e32f89	c63718e2-d2a6-4822-ac99-2e05d0912be4	Já encaminhei a troca.	2026-07-16 16:16:13.138908+00	1
ac73e9cd-f281-4670-bd37-ab6ffa8e7ffc	110b60c5-3fd0-4906-ad79-38c5966f9021	c63718e2-d2a6-4822-ac99-2e05d0912be4	TEste	2026-07-16 17:55:16.17812+00	1
b8ae0ebc-d4f2-48cc-97f4-8bc8ed802a12	c9c4241f-c5a1-46e7-b289-16ec12496a64	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	O problema ocorre em todas as chamadas recebidas.	2026-07-16 18:07:48.664619+00	1
3312fa99-06ae-4fd6-b36b-e9ed4e377e9f	c9c4241f-c5a1-46e7-b289-16ec12496a64	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	Pode verificar?	2026-07-16 18:13:50.485297+00	1
a0f950a4-cbe7-41b7-9ab6-83fe21a471af	c9c4241f-c5a1-46e7-b289-16ec12496a64	c63718e2-d2a6-4822-ac99-2e05d0912be4	Vo ver	2026-07-16 18:49:57.41094+00	1
e50ac5a6-d601-4d5a-9be6-0b8718cb1083	d47ec655-3284-40bf-aac7-d95c65e32f89	c63718e2-d2a6-4822-ac99-2e05d0912be4	teste	2026-07-16 21:36:46.031847+00	1
5724517f-e6e5-4245-92f4-5dec97eea67e	d47ec655-3284-40bf-aac7-d95c65e32f89	c63718e2-d2a6-4822-ac99-2e05d0912be4	teste	2026-07-16 21:52:07.304231+00	1
2ed7d9bb-916d-4d8a-91d1-49a0e794bfdf	2b71b542-22ef-4a10-b60f-9ac469e4ca0c	c63718e2-d2a6-4822-ac99-2e05d0912be4	Teste	2026-07-16 21:54:24.046531+00	1
a881104a-63c1-4216-968f-9f3fb1a61726	2b71b542-22ef-4a10-b60f-9ac469e4ca0c	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	Teste	2026-07-16 21:54:50.452858+00	1
131dd9f1-f50a-455c-b595-512143fb7cea	09edaa9f-893f-4c9b-966e-e17c985a5b20	7fbf6731-8774-4bc8-acfe-b386a54a6bf8	Teste de atualização	2026-07-20 17:28:54.452245+00	1
3a936d3f-c474-4d39-b426-06009f0786c6	d47ec655-3284-40bf-aac7-d95c65e32f89	c63718e2-d2a6-4822-ac99-2e05d0912be4	Me envie uma foto do vazamento por gentileza	2026-07-20 23:56:41.810774+00	1
78a22ac0-1331-4e01-a1b6-a16f14fd9610	78709640-07c2-4fdf-8d13-b536020f9289	2e38f0b9-18a0-446c-9ad7-009457fd86d6	deu?	2026-07-27 14:56:40.708518+00	1
1623f547-7c65-4153-8df6-5c39f8afe7d2	78709640-07c2-4fdf-8d13-b536020f9289	7bcbd08e-4607-4cd0-a9b6-d492002c65a0	Não deu	2026-07-27 14:57:11.058932+00	1
3c9a15d0-c364-4b7e-ac20-722251def3a3	e8be5415-6203-4b08-a6c2-fa52bae5fe26	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	Sumiu	2026-07-28 03:53:13.872949+00	1
2e57870a-19fe-4720-8048-e570bb475d9b	e8be5415-6203-4b08-a6c2-fa52bae5fe26	c63718e2-d2a6-4822-ac99-2e05d0912be4	Espera ai kct	2026-07-28 03:54:03.546937+00	1
8d3991ab-2457-4a69-b3ed-96582cc9ca63	cf8d4ef1-741c-45a7-91ea-2d0a73e7ed18	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	Pede pra parar o barulho lá seu bosta	2026-07-28 03:56:04.947826+00	1
a6e4eadd-2b64-4ee1-8d4a-b21c1cfe8781	79669645-30c6-4dee-9f36-a07b0ac232a5	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	asdas	2026-07-28 03:58:01.053594+00	1
\.


--
-- Data for Name: request_status_history; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.request_status_history (id, request_id, previous_status, new_status, changed_by_user_id, reason, created_at) FROM stdin;
17ee5dbb-b02a-4427-806c-643c60eb4758	022be16b-d325-4623-b95f-f45304cbca2e	\N	1	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	\N	2026-07-16 14:18:40.0877+00
88cbc846-ae68-4005-8981-706f774ad295	d47ec655-3284-40bf-aac7-d95c65e32f89	\N	1	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	\N	2026-07-16 14:18:40.261486+00
a92d964c-f7d5-4f92-bb24-549fbf1311d2	d47ec655-3284-40bf-aac7-d95c65e32f89	1	2	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	Atendimento iniciado.	2026-07-16 16:23:55.61829+00
8d79ed2a-4957-4fff-bd09-5e3ad6e87f8e	d47ec655-3284-40bf-aac7-d95c65e32f89	2	3	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	\N	2026-07-16 16:23:55.784358+00
231cf8d5-ad5b-4a1e-b453-3007b383b68b	d47ec655-3284-40bf-aac7-d95c65e32f89	3	5	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	Atendimento concluído.	2026-07-16 16:23:55.796637+00
666a9859-d00b-4b88-89a7-e65dfb594c63	d47ec655-3284-40bf-aac7-d95c65e32f89	5	2	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	Solicitação reaberta.	2026-07-16 16:23:55.806515+00
6810dc01-b1ae-414f-9c81-8f756cd9cdad	110b60c5-3fd0-4906-ad79-38c5966f9021	\N	1	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-16 17:55:03.469147+00
b82f3ab2-cbb2-41de-95c3-53e7545db529	c9c4241f-c5a1-46e7-b289-16ec12496a64	\N	1	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	\N	2026-07-16 18:07:40.415884+00
eb586ea8-2d0d-47c0-855d-d5ecfd554701	c9c4241f-c5a1-46e7-b289-16ec12496a64	1	2	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	Solicitacao assumida pela gestao.	2026-07-16 18:08:34.125091+00
94ef03e9-ca94-4ff9-bf7a-6a29503dd205	c9c4241f-c5a1-46e7-b289-16ec12496a64	2	3	c63718e2-d2a6-4822-ac99-2e05d0912be4	Validacao do acesso Manager de Lisandro.	2026-07-16 18:39:57.206936+00
2a4cad80-9b18-4e48-ba8d-c88d8b8383fb	c9c4241f-c5a1-46e7-b289-16ec12496a64	3	2	c63718e2-d2a6-4822-ac99-2e05d0912be4	Estado restaurado apos validacao.	2026-07-16 18:39:57.260405+00
e7ddd55f-b113-4c8b-9b3e-cf2846f97558	c9c4241f-c5a1-46e7-b289-16ec12496a64	2	3	c63718e2-d2a6-4822-ac99-2e05d0912be4	Ve aiu	2026-07-16 18:50:16.543602+00
66105887-ed52-4cbc-9653-4e9d24267638	c9c4241f-c5a1-46e7-b289-16ec12496a64	3	5	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-16 18:55:36.728851+00
d5d142b0-9e3e-4f37-ad26-cf3ee1baed8f	110b60c5-3fd0-4906-ad79-38c5966f9021	1	6	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-16 19:16:03.766691+00
a9b96b4d-cbf8-4021-834a-e499bc76358f	3b1b06e6-bd29-45df-8a96-81184fb0c266	\N	1	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	\N	2026-07-16 19:18:53.509974+00
d556afb5-8458-4069-b491-fdb327032d80	022be16b-d325-4623-b95f-f45304cbca2e	1	2	c63718e2-d2a6-4822-ac99-2e05d0912be4	Verificando com o prestador de serviço	2026-07-16 20:33:27.088832+00
ec3e6c51-ec5f-4265-a537-3a3c886594c1	d47ec655-3284-40bf-aac7-d95c65e32f89	2	5	c63718e2-d2a6-4822-ac99-2e05d0912be4	Vazamento arrumado	2026-07-16 20:34:05.453592+00
23f03d51-5832-4223-9dbc-b946223a5f7a	2b71b542-22ef-4a10-b60f-9ac469e4ca0c	\N	1	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	\N	2026-07-16 20:41:48.922716+00
e3fc9606-77ae-4f56-90b7-04647c741c00	022be16b-d325-4623-b95f-f45304cbca2e	2	4	c63718e2-d2a6-4822-ac99-2e05d0912be4	Aguardando prestador de serviço	2026-07-16 20:44:17.46774+00
adcb7aea-24fa-430d-9338-1db969313cda	022be16b-d325-4623-b95f-f45304cbca2e	4	6	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-16 20:44:53.573406+00
67dc14d7-39d8-4147-a9c7-3ad2b7c3c42f	3b1b06e6-bd29-45df-8a96-81184fb0c266	1	2	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-16 21:32:19.703006+00
030659aa-44af-4d66-b722-e72a691fc7ed	d47ec655-3284-40bf-aac7-d95c65e32f89	5	1	c63718e2-d2a6-4822-ac99-2e05d0912be4	Teste	2026-07-16 21:35:19.043356+00
8e52499b-f1fc-468b-bee3-ede124894157	022be16b-d325-4623-b95f-f45304cbca2e	6	1	c63718e2-d2a6-4822-ac99-2e05d0912be4	lçkj	2026-07-16 21:35:55.364669+00
69076d67-af8c-4422-8ab2-175670d3c67e	d47ec655-3284-40bf-aac7-d95c65e32f89	1	2	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-16 21:39:37.436709+00
88519073-8a8d-4342-a717-f137604725f3	d47ec655-3284-40bf-aac7-d95c65e32f89	2	4	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-17 22:06:00.81314+00
c8e3af8e-de10-43f7-a044-2fe5d3e4e315	09edaa9f-893f-4c9b-966e-e17c985a5b20	\N	1	7fbf6731-8774-4bc8-acfe-b386a54a6bf8	\N	2026-07-20 17:28:41.344833+00
57b580e1-ab7b-4af8-bfa4-0ced2a364664	10bb8ebb-26a6-4606-95e2-97b09a132a99	\N	1	368e5e05-df65-4a38-8c74-f29086b6029a	\N	2026-07-20 17:56:48.035007+00
31205fe8-5bed-45b8-aaad-ccae978f7da8	57f397eb-c560-430d-89f7-4815aa7ae115	\N	1	9fc09869-4ffd-4d49-83c6-2aff3d89e2a5	\N	2026-07-20 17:58:50.416675+00
1030a631-2c41-42d3-83ce-8473172ed650	d47ec655-3284-40bf-aac7-d95c65e32f89	4	2	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-22 17:00:10.988743+00
fc5041d0-4375-4f8d-aebd-c8b871a74540	d47ec655-3284-40bf-aac7-d95c65e32f89	2	4	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-22 17:00:30.311562+00
506dc95a-c742-4413-a725-fcc0bbe79466	022be16b-d325-4623-b95f-f45304cbca2e	1	5	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-26 02:06:16.896436+00
67337a42-3d16-43bd-b257-b81281a43e21	34d1a361-948b-4dff-ae05-1b4a6016be40	\N	1	2e38f0b9-18a0-446c-9ad7-009457fd86d6	\N	2026-07-27 14:48:40.905102+00
84600493-972e-4b25-9f9a-fb49cb662594	78709640-07c2-4fdf-8d13-b536020f9289	\N	1	2e38f0b9-18a0-446c-9ad7-009457fd86d6	\N	2026-07-27 14:50:01.635251+00
708aa7b8-34e9-47b7-9d7d-2435adaffafd	78709640-07c2-4fdf-8d13-b536020f9289	1	2	7bcbd08e-4607-4cd0-a9b6-d492002c65a0	\N	2026-07-27 14:57:50.834211+00
1007509a-2251-41bb-984c-c3a842e768d6	78709640-07c2-4fdf-8d13-b536020f9289	2	4	7bcbd08e-4607-4cd0-a9b6-d492002c65a0	Agendada manutenção com o prestador de serviço da porta	2026-07-27 14:58:14.717993+00
2793ce32-dd11-4c4f-bf12-88b1f68e5f7e	e8be5415-6203-4b08-a6c2-fa52bae5fe26	\N	1	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	\N	2026-07-28 03:53:04.678659+00
9b6e517e-9707-461f-ad9f-bc8f00eb481b	e8be5415-6203-4b08-a6c2-fa52bae5fe26	1	2	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-28 03:53:38.206467+00
5330a68b-1425-4695-af57-4ffa4914e881	e8be5415-6203-4b08-a6c2-fa52bae5fe26	2	4	c63718e2-d2a6-4822-ac99-2e05d0912be4	Repassado à portaria, aguardando verificação das cameras.	2026-07-28 03:53:53.661468+00
44bd93fe-6d5d-474e-b9f0-55948d483c87	e8be5415-6203-4b08-a6c2-fa52bae5fe26	4	5	c63718e2-d2a6-4822-ac99-2e05d0912be4	Achou	2026-07-28 03:54:29.992149+00
bdefaf2b-aec2-42b0-a551-347a747572a6	cf8d4ef1-741c-45a7-91ea-2d0a73e7ed18	\N	1	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	\N	2026-07-28 03:55:32.097755+00
206967f1-287a-499a-b4cd-194ebc5c72a5	cf8d4ef1-741c-45a7-91ea-2d0a73e7ed18	1	5	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-28 03:57:00.732837+00
f4174867-d737-48da-a71a-3d0b2beac902	79669645-30c6-4dee-9f36-a07b0ac232a5	\N	1	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	\N	2026-07-28 03:57:57.156605+00
154ddf56-d88b-4f48-bc9c-e4ef9c01cd47	79669645-30c6-4dee-9f36-a07b0ac232a5	1	6	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-28 03:59:17.066894+00
326bb4b0-0f92-4867-8bf7-89feb9f2fd63	79669645-30c6-4dee-9f36-a07b0ac232a5	6	1	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-28 03:59:42.799634+00
4bfda7f4-3d78-46c7-83fb-17384951d15b	79669645-30c6-4dee-9f36-a07b0ac232a5	1	6	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-28 03:59:59.524545+00
5df11dbf-62e8-4bc8-94ad-1da3b3c8d570	974b54e7-acf8-4189-b718-a1476a29b563	\N	1	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	\N	2026-07-28 04:48:49.905298+00
03b37503-5709-4ab0-b7ab-aae98dfaf954	974b54e7-acf8-4189-b718-a1476a29b563	1	5	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-28 04:49:10.84122+00
f3f68d62-0845-40de-acd4-8d762bd2ef0d	974b54e7-acf8-4189-b718-a1476a29b563	5	1	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-28 04:49:25.942181+00
09a7a4cc-6bc4-4ceb-8c3e-95ac51d1543a	974b54e7-acf8-4189-b718-a1476a29b563	1	6	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	2026-07-28 04:49:34.56871+00
6c7c8013-a1ee-41c7-a292-638420ba9d5d	6e273245-7079-4096-ba3b-028d61710b2f	\N	1	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	\N	2026-07-28 04:50:35.34283+00
\.


--
-- Data for Name: requests; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.requests (id, condominium_id, author_user_id, target_unit_id, category_id, title, description, status, priority, created_at, updated_at, resolved_at, source) FROM stdin;
3b1b06e6-bd29-45df-8a96-81184fb0c266	e61d21e3-8cab-47e3-bd28-341a78a457a9	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	\N	60011118-f2f0-4135-b39a-76f963bbed0b	saad	asdasda	2	1	2026-07-16 19:18:53.509974+00	2026-07-16 21:32:19.703006+00	\N	1
09edaa9f-893f-4c9b-966e-e17c985a5b20	57f10b5c-f01b-401e-af00-879611ac61c3	7fbf6731-8774-4bc8-acfe-b386a54a6bf8	\N	1dcfb512-e79c-4eda-b2a6-38a97081ee8a	Solicitação de teste	Não sei, to testando	1	1	2026-07-20 17:28:41.344833+00	2026-07-20 17:28:41.344833+00	\N	1
10bb8ebb-26a6-4606-95e2-97b09a132a99	57f10b5c-f01b-401e-af00-879611ac61c3	368e5e05-df65-4a38-8c74-f29086b6029a	f07f83c3-4a17-45bc-8cf1-c839a2a1e680	1dcfb512-e79c-4eda-b2a6-38a97081ee8a	Teste do central	asdasd	1	1	2026-07-20 17:56:48.035007+00	2026-07-20 17:56:48.035007+00	\N	1
57f397eb-c560-430d-89f7-4815aa7ae115	ba468e96-7cb3-4150-8a1d-d4530f212edf	9fc09869-4ffd-4d49-83c6-2aff3d89e2a5	f10b2f8c-6242-4775-8497-66057b331408	e7fa5ae9-611b-48f1-b0a9-13f262f845cc	Teste no inga	szdadadasd	1	1	2026-07-20 17:58:50.416675+00	2026-07-20 17:58:50.416675+00	\N	1
c9c4241f-c5a1-46e7-b289-16ec12496a64	e61d21e3-8cab-47e3-bd28-341a78a457a9	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	\N	60011118-f2f0-4135-b39a-76f963bbed0b	Interfone com ruido	O interfone apresenta ruido durante as chamadas.	5	3	2026-07-16 18:07:40.415884+00	2026-07-16 18:55:36.728851+00	2026-07-16 18:55:36.728851+00	1
110b60c5-3fd0-4906-ad79-38c5966f9021	e61d21e3-8cab-47e3-bd28-341a78a457a9	c63718e2-d2a6-4822-ac99-2e05d0912be4	\N	60011118-f2f0-4135-b39a-76f963bbed0b	Cano quebrado	No bloco 2	6	1	2026-07-16 17:55:03.469147+00	2026-07-16 19:16:03.766691+00	\N	1
2b71b542-22ef-4a10-b60f-9ac469e4ca0c	e61d21e3-8cab-47e3-bd28-341a78a457a9	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	\N	45e58a41-be3f-45d5-907d-ab5dc8a1952d	Nao consigo subir a rampa	asdasdasdasd	1	3	2026-07-16 20:41:48.922716+00	2026-07-22 16:59:37.947912+00	\N	1
d47ec655-3284-40bf-aac7-d95c65e32f89	e61d21e3-8cab-47e3-bd28-341a78a457a9	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	8d0ce61f-d526-4326-9091-445d9396509e	016320f0-869e-48ea-8b6e-cdd4194bb51c	Vazamento próximo à unidade	Existe água no corredor.	4	3	2026-07-16 14:18:40.261486+00	2026-07-22 17:00:30.311562+00	\N	1
022be16b-d325-4623-b95f-f45304cbca2e	e61d21e3-8cab-47e3-bd28-341a78a457a9	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	\N	016320f0-869e-48ea-8b6e-cdd4194bb51c	Lâmpada queimada	A lâmpada próxima ao elevador não está funcionando.	5	1	2026-07-16 14:18:40.0877+00	2026-07-26 02:06:16.896436+00	2026-07-26 02:06:16.896436+00	1
34d1a361-948b-4dff-ae05-1b4a6016be40	59d9844b-2207-4884-ac68-43357392b2c3	2e38f0b9-18a0-446c-9ad7-009457fd86d6	\N	dfcbdf64-ecc9-4279-8996-3d3e3c039f13	Trinco quebrado	tem um trinco quebrado	1	1	2026-07-27 14:48:40.905102+00	2026-07-27 14:48:40.905102+00	\N	1
78709640-07c2-4fdf-8d13-b536020f9289	59d9844b-2207-4884-ac68-43357392b2c3	2e38f0b9-18a0-446c-9ad7-009457fd86d6	8e94ae9c-99fb-4729-b4ff-e2665ec47361	dfcbdf64-ecc9-4279-8996-3d3e3c039f13	Porta não fecha	porta	4	2	2026-07-27 14:50:01.635251+00	2026-07-27 14:58:14.717993+00	\N	1
e8be5415-6203-4b08-a6c2-fa52bae5fe26	e61d21e3-8cab-47e3-bd28-341a78a457a9	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	\N	add6212f-fc70-4214-9628-483a102a37b6	Minha encomenda sumiu	Sumiu essa bosta	5	3	2026-07-28 03:53:04.678659+00	2026-07-28 03:54:29.992149+00	2026-07-28 03:54:29.992149+00	1
cf8d4ef1-741c-45a7-91ea-2d0a73e7ed18	e61d21e3-8cab-47e3-bd28-341a78a457a9	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	\N	016320f0-869e-48ea-8b6e-cdd4194bb51c	Nao congiso dormir	Preciso acordar cedo amanhã	5	1	2026-07-28 03:55:32.097755+00	2026-07-28 03:57:00.732837+00	2026-07-28 03:57:00.732837+00	1
79669645-30c6-4dee-9f36-a07b0ac232a5	e61d21e3-8cab-47e3-bd28-341a78a457a9	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	\N	45e58a41-be3f-45d5-907d-ab5dc8a1952d	werwe	rwerwer	6	1	2026-07-28 03:57:57.156605+00	2026-07-28 03:59:59.524545+00	\N	1
974b54e7-acf8-4189-b718-a1476a29b563	e61d21e3-8cab-47e3-bd28-341a78a457a9	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	\N	016320f0-869e-48ea-8b6e-cdd4194bb51c	test	tets	6	1	2026-07-28 04:48:49.905298+00	2026-07-28 04:49:34.56871+00	\N	1
6e273245-7079-4096-ba3b-028d61710b2f	e61d21e3-8cab-47e3-bd28-341a78a457a9	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	4a8952b1-f967-424c-ad27-ad6330e3480f	4954855b-3b86-4a53-b725-442aa68d47b4	sdfas	dasd	1	1	2026-07-28 04:50:35.34283+00	2026-07-28 04:50:35.34283+00	\N	1
\.


--
-- Data for Name: unit_memberships; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.unit_memberships (id, user_id, unit_id, relationship_type, is_resident, is_primary_residence, is_active, started_at, ended_at, created_at) FROM stdin;
443d8044-8e4a-456d-92d1-ec9a40bc440c	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	60acc110-5fba-41e9-9b5b-09a3ed759766	1	t	t	t	2026-07-16 13:49:00.758604+00	\N	2026-07-16 13:49:00.758604+00
205980d3-c195-4fb7-9af4-18b645d957f4	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	3f59b5e1-3bee-4e8c-a6d4-c2327253fb92	3	t	f	t	2026-07-16 19:30:48.344076+00	\N	2026-07-16 19:30:48.344076+00
d7718076-cb0a-4c6e-9327-cdb8f7b3f44e	10a2a96c-8f5f-4a05-a750-6a5a928aebea	87187fe7-78bf-4b51-9396-191b5fc4d61c	2	t	t	t	2026-07-16 20:47:55.386261+00	\N	2026-07-16 20:47:55.386261+00
0e070f7d-12cb-4cbc-bb5b-47762b11910b	65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	f5dbae6d-d3b1-4e4d-be50-ab1aacae9aa4	2	t	t	t	2026-07-17 02:05:51.382968+00	\N	2026-07-17 02:05:51.382968+00
123212a9-763d-4cf6-af70-ee5301d8da37	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	60acc110-5fba-41e9-9b5b-09a3ed759766	2	t	f	f	2026-07-16 13:49:00.881441+00	2026-07-17 02:35:12.650303+00	2026-07-16 13:49:00.881441+00
fde0573c-0f92-403f-a94a-03e3472b74ca	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	60acc110-5fba-41e9-9b5b-09a3ed759766	3	t	f	f	2026-07-16 13:49:00.908215+00	2026-07-17 02:36:01.003733+00	2026-07-16 13:49:00.908215+00
59c9e03e-08a0-4101-92da-b315ba0756cd	cf8cdc65-08b5-4631-b2f8-784c27a5dc35	f5dbae6d-d3b1-4e4d-be50-ab1aacae9aa4	1	f	f	t	2026-07-17 02:36:44.673749+00	\N	2026-07-17 02:36:44.673749+00
76812017-6925-4f50-b959-0cdbaedc91c0	c08f02ee-2a03-4214-94a5-c5956fbe0dff	3f59b5e1-3bee-4e8c-a6d4-c2327253fb92	1	t	f	t	2026-07-16 19:43:55.6458+00	\N	2026-07-16 19:43:55.6458+00
31fa5360-9b6c-40ff-96bc-5569dbd6f4c9	7fbf6731-8774-4bc8-acfe-b386a54a6bf8	e9f4a1e4-902d-4f7e-aa84-0c1920d14e75	1	t	f	t	2026-07-20 16:41:32.002735+00	\N	2026-07-20 16:41:32.002735+00
38f73417-fd4d-4287-924a-9a046b659b06	8e3ad79b-fba4-462e-a913-9cd31cfdfe1f	a2789f25-12f0-44f7-bafd-fb01061d8357	1	f	f	t	2026-07-20 17:30:18.575215+00	\N	2026-07-20 17:30:18.575215+00
1cb45703-fef9-4509-b7f3-732ade2831f3	1bfdc06d-9f08-400e-a3da-a227d747a5fa	53982823-59dd-48e1-b3d5-8f602586d176	2	t	f	t	2026-07-20 17:30:53.670058+00	\N	2026-07-20 17:30:53.670058+00
3b48facf-3e59-49d2-a78b-e157b7141852	1e80376a-c6b4-445b-8a6f-0ced2d85f8c2	e9f4a1e4-902d-4f7e-aa84-0c1920d14e75	1	f	f	t	2026-07-20 17:31:10.796834+00	\N	2026-07-20 17:31:10.796834+00
f518e1f6-aa78-4a11-ad1b-f6d29cdee45d	3526eabc-0c28-40f4-8ae4-24b1fcdcae51	ee9e13e7-2158-435f-bf6e-943940ea1975	1	f	f	t	2026-07-20 17:55:18.487753+00	\N	2026-07-20 17:55:18.487753+00
5fe21daa-9055-4135-b51e-6190077415d1	368e5e05-df65-4a38-8c74-f29086b6029a	f07f83c3-4a17-45bc-8cf1-c839a2a1e680	1	f	f	t	2026-07-20 17:56:18.531822+00	\N	2026-07-20 17:56:18.531822+00
93ccfc6b-2e1c-4ac7-9970-c039bfcca302	9fc09869-4ffd-4d49-83c6-2aff3d89e2a5	f10b2f8c-6242-4775-8497-66057b331408	1	t	f	t	2026-07-20 17:57:44.115522+00	\N	2026-07-20 17:57:44.115522+00
1f88ca49-f221-4266-b7ce-2a5e1053af20	2e38f0b9-18a0-446c-9ad7-009457fd86d6	8e94ae9c-99fb-4729-b4ff-e2665ec47361	1	t	f	t	2026-07-27 14:49:29.587648+00	\N	2026-07-27 14:49:29.587648+00
adc80756-ebf3-4b04-8a5c-fed3668446a0	10a2a96c-8f5f-4a05-a750-6a5a928aebea	4fb8b42d-2044-4229-92a8-3110df712bc7	1	t	t	t	2026-07-28 03:55:02.76059+00	\N	2026-07-28 03:55:02.76059+00
0b5d3112-3963-475f-b853-fe041c62967b	5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	4a8952b1-f967-424c-ad27-ad6330e3480f	1	f	f	t	2026-07-28 04:50:25.860245+00	\N	2026-07-28 04:50:25.860245+00
\.


--
-- Data for Name: units; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.units (id, condominium_id, identifier, floor, description, is_active, created_at, updated_at, block_id) FROM stdin;
8d0ce61f-d526-4326-9091-445d9396509e	e61d21e3-8cab-47e3-bd28-341a78a457a9	305	3	Apartamento 305	t	2026-07-16 12:25:20.385684+00	2026-07-16 12:25:20.385684+00	9233bd9d-ea96-46d2-af69-636ed7c827c9
87187fe7-78bf-4b51-9396-191b5fc4d61c	e61d21e3-8cab-47e3-bd28-341a78a457a9	304	3	\N	t	2026-07-16 19:36:48.641794+00	2026-07-16 19:36:48.641794+00	13ce7422-5017-494f-bed7-cd7fc4bd51f2
f5dbae6d-d3b1-4e4d-be50-ab1aacae9aa4	e61d21e3-8cab-47e3-bd28-341a78a457a9	305	3	\N	t	2026-07-16 12:25:20.663136+00	2026-07-17 02:07:19.965689+00	4ecea3c5-e53b-4b57-b9fc-5778a6332272
60acc110-5fba-41e9-9b5b-09a3ed759766	e61d21e3-8cab-47e3-bd28-341a78a457a9	102	\N	\N	t	2026-07-16 12:25:20.620615+00	2026-07-17 02:09:36.083635+00	9233bd9d-ea96-46d2-af69-636ed7c827c9
9d4ea7e1-4ad8-4f4e-bc93-e5a3353d847b	e61d21e3-8cab-47e3-bd28-341a78a457a9	604	\N	teste	t	2026-07-17 02:11:34.547784+00	2026-07-17 02:11:34.547784+00	4ecea3c5-e53b-4b57-b9fc-5778a6332272
3f59b5e1-3bee-4e8c-a6d4-c2327253fb92	e61d21e3-8cab-47e3-bd28-341a78a457a9	707	Terreo	Unidade criada para validar o fluxo inicial de gestao.	t	2026-07-16 19:30:48.207882+00	2026-07-17 02:38:19.956835+00	ce8429ae-06b3-4ee8-883c-3af23ffed690
a7aa7e31-974c-4211-a9e5-6e41fbe799ae	e61d21e3-8cab-47e3-bd28-341a78a457a9	Josué	\N	Testando	t	2026-07-17 02:46:43.630235+00	2026-07-17 02:46:43.630235+00	4ecea3c5-e53b-4b57-b9fc-5778a6332272
17c68ac6-2c7e-45cb-89ca-a66fe21ca755	e61d21e3-8cab-47e3-bd28-341a78a457a9	305	\N	s	t	2026-07-17 02:12:35.194863+00	2026-07-17 02:59:24.865251+00	13ce7422-5017-494f-bed7-cd7fc4bd51f2
e9f4a1e4-902d-4f7e-aa84-0c1920d14e75	57f10b5c-f01b-401e-af00-879611ac61c3	101	\N	\N	t	2026-07-20 16:39:30.470512+00	2026-07-20 16:39:30.470512+00	\N
ee9e13e7-2158-435f-bf6e-943940ea1975	57f10b5c-f01b-401e-af00-879611ac61c3	101	\N	\N	t	2026-07-20 16:41:57.459226+00	2026-07-20 16:41:57.459226+00	53d8d657-78f8-4641-9ff7-99f200cb45ec
53982823-59dd-48e1-b3d5-8f602586d176	ba468e96-7cb3-4150-8a1d-d4530f212edf	101	\N	\N	t	2026-07-20 16:51:13.481867+00	2026-07-20 16:51:13.481867+00	0a1dcf4f-d3a8-49ca-a725-2364b80c53dc
f10b2f8c-6242-4775-8497-66057b331408	ba468e96-7cb3-4150-8a1d-d4530f212edf	102	\N	\N	t	2026-07-20 16:52:07.822111+00	2026-07-20 16:52:07.822111+00	0a1dcf4f-d3a8-49ca-a725-2364b80c53dc
853a38ce-dc84-4561-95c8-e9efcbba8b39	ba468e96-7cb3-4150-8a1d-d4530f212edf	103	\N	\N	t	2026-07-20 17:02:07.983617+00	2026-07-20 17:02:07.983617+00	0a1dcf4f-d3a8-49ca-a725-2364b80c53dc
a2789f25-12f0-44f7-bafd-fb01061d8357	ba468e96-7cb3-4150-8a1d-d4530f212edf	201	\N	\N	t	2026-07-20 17:24:50.253019+00	2026-07-20 17:24:50.253019+00	d190e2aa-86f3-4f14-aa68-abb8651ea49c
f07f83c3-4a17-45bc-8cf1-c839a2a1e680	57f10b5c-f01b-401e-af00-879611ac61c3	202	\N	\N	t	2026-07-20 17:55:58.211685+00	2026-07-20 17:55:58.211685+00	c07f8a4c-6f3e-49a0-9c5d-33a114929cba
bb95517d-a462-4447-ab9e-1a45a470b8f8	ba468e96-7cb3-4150-8a1d-d4530f212edf	404	\N	\N	t	2026-07-20 18:01:34.555805+00	2026-07-20 18:01:34.555805+00	d190e2aa-86f3-4f14-aa68-abb8651ea49c
8e94ae9c-99fb-4729-b4ff-e2665ec47361	59d9844b-2207-4884-ac68-43357392b2c3	101	\N	\N	t	2026-07-27 12:25:04.090763+00	2026-07-27 12:25:04.090763+00	669a3da0-432b-49e4-9103-09353a1caf48
060fcf38-10cc-4916-ac6d-69986ae0d84b	e61d21e3-8cab-47e3-bd28-341a78a457a9	201	2	\N	t	2026-07-28 03:48:53.024274+00	2026-07-28 03:48:53.024274+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
0683a17a-223f-4ade-bc63-c1c2548550ac	e61d21e3-8cab-47e3-bd28-341a78a457a9	203	2	\N	t	2026-07-28 03:48:53.023625+00	2026-07-28 03:48:53.023625+00	f63b2806-634c-41a3-89db-43ffab39e38e
0cb674f6-43ae-49ae-9a80-682b3b4ffa84	e61d21e3-8cab-47e3-bd28-341a78a457a9	404	4	\N	t	2026-07-28 03:48:53.024559+00	2026-07-28 03:48:53.024559+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
0f98d841-1038-4bd2-b3d4-b2909b6da3b2	e61d21e3-8cab-47e3-bd28-341a78a457a9	106	1	\N	t	2026-07-28 03:48:53.024258+00	2026-07-28 03:48:53.024258+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
11e93439-c8a5-4b43-a3bc-fe02e37c84e4	e61d21e3-8cab-47e3-bd28-341a78a457a9	301	3	\N	t	2026-07-28 03:48:53.023678+00	2026-07-28 03:48:53.023678+00	f63b2806-634c-41a3-89db-43ffab39e38e
14abaaa5-5257-41f5-b864-3bada7dc5be5	e61d21e3-8cab-47e3-bd28-341a78a457a9	304	3	\N	t	2026-07-28 03:48:53.024469+00	2026-07-28 03:48:53.024469+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
1b49521d-2031-47a9-a0da-6af08acbb4dc	e61d21e3-8cab-47e3-bd28-341a78a457a9	302	3	\N	t	2026-07-28 03:48:53.024443+00	2026-07-28 03:48:53.024443+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
302b8518-79b7-45f0-ace8-fa3c9b0016fe	e61d21e3-8cab-47e3-bd28-341a78a457a9	202	2	\N	t	2026-07-28 03:48:53.023612+00	2026-07-28 03:48:53.023612+00	f63b2806-634c-41a3-89db-43ffab39e38e
4a8952b1-f967-424c-ad27-ad6330e3480f	e61d21e3-8cab-47e3-bd28-341a78a457a9	603	6	\N	t	2026-07-28 03:48:53.024089+00	2026-07-28 03:48:53.024089+00	f63b2806-634c-41a3-89db-43ffab39e38e
4d33535c-db74-4bb2-9b70-d17a5c14e13b	e61d21e3-8cab-47e3-bd28-341a78a457a9	601	6	\N	t	2026-07-28 03:48:53.024024+00	2026-07-28 03:48:53.024024+00	f63b2806-634c-41a3-89db-43ffab39e38e
4fb8b42d-2044-4229-92a8-3110df712bc7	e61d21e3-8cab-47e3-bd28-341a78a457a9	604	6	\N	t	2026-07-28 03:48:53.024108+00	2026-07-28 03:48:53.024108+00	f63b2806-634c-41a3-89db-43ffab39e38e
503b767f-e230-44fb-8972-9c40bd0dde08	e61d21e3-8cab-47e3-bd28-341a78a457a9	402	4	\N	t	2026-07-28 03:48:53.024533+00	2026-07-28 03:48:53.024533+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
523df8e0-e410-4054-bff8-92507d44a58b	e61d21e3-8cab-47e3-bd28-341a78a457a9	304	3	\N	t	2026-07-28 03:48:53.023735+00	2026-07-28 03:48:53.023735+00	f63b2806-634c-41a3-89db-43ffab39e38e
5c3dd57a-1420-45d4-b5a4-048ad96592f1	e61d21e3-8cab-47e3-bd28-341a78a457a9	303	3	\N	t	2026-07-28 03:48:53.024456+00	2026-07-28 03:48:53.024456+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
674f0850-9418-4839-a686-8fd681785f5e	e61d21e3-8cab-47e3-bd28-341a78a457a9	303	3	\N	t	2026-07-28 03:48:53.023723+00	2026-07-28 03:48:53.023723+00	f63b2806-634c-41a3-89db-43ffab39e38e
67758301-6366-40e2-91d3-5d4b9d910080	e61d21e3-8cab-47e3-bd28-341a78a457a9	102	1	\N	t	2026-07-28 03:48:53.02414+00	2026-07-28 03:48:53.02414+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
6bcbb75f-8ad9-42b6-8c86-3f207fbcbb1a	e61d21e3-8cab-47e3-bd28-341a78a457a9	401	4	\N	t	2026-07-28 03:48:53.024519+00	2026-07-28 03:48:53.024519+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
6c8c326a-69e4-458e-aad9-a4f88cef87c4	e61d21e3-8cab-47e3-bd28-341a78a457a9	104	1	\N	t	2026-07-28 03:48:53.024202+00	2026-07-28 03:48:53.024202+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
6e1d8d7c-5ea9-4c94-a9da-c24e76cb19ad	e61d21e3-8cab-47e3-bd28-341a78a457a9	202	2	\N	t	2026-07-28 03:48:53.024288+00	2026-07-28 03:48:53.024288+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
6e392ed0-17d5-469f-b679-fe5fd1aefc42	e61d21e3-8cab-47e3-bd28-341a78a457a9	105	1	\N	t	2026-07-28 03:48:53.024241+00	2026-07-28 03:48:53.024241+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
71d05103-28f4-4f51-bca5-fa5529c89d4e	e61d21e3-8cab-47e3-bd28-341a78a457a9	503	5	\N	t	2026-07-28 03:48:53.023973+00	2026-07-28 03:48:53.023973+00	f63b2806-634c-41a3-89db-43ffab39e38e
74738f03-c578-487f-ab61-6172867beed6	e61d21e3-8cab-47e3-bd28-341a78a457a9	204	2	\N	t	2026-07-28 03:48:53.024377+00	2026-07-28 03:48:53.024377+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
7e86f552-12f1-49cc-9c1f-18c36b3d341a	e61d21e3-8cab-47e3-bd28-341a78a457a9	502	5	\N	t	2026-07-28 03:48:53.023949+00	2026-07-28 03:48:53.023949+00	f63b2806-634c-41a3-89db-43ffab39e38e
7fba8219-1074-4550-87a1-30003471d379	e61d21e3-8cab-47e3-bd28-341a78a457a9	401	4	\N	t	2026-07-28 03:48:53.02379+00	2026-07-28 03:48:53.02379+00	f63b2806-634c-41a3-89db-43ffab39e38e
81c99938-6c1c-46d8-9f24-5ddf18f7026f	e61d21e3-8cab-47e3-bd28-341a78a457a9	301	3	\N	t	2026-07-28 03:48:53.024428+00	2026-07-28 03:48:53.024428+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
82463122-096e-4d02-9fe9-dae2939000fc	e61d21e3-8cab-47e3-bd28-341a78a457a9	201	2	\N	t	2026-07-28 03:48:53.023599+00	2026-07-28 03:48:53.023599+00	f63b2806-634c-41a3-89db-43ffab39e38e
84e14e81-75e6-44bb-ab9a-db195c3c5f68	e61d21e3-8cab-47e3-bd28-341a78a457a9	306	3	\N	t	2026-07-28 03:48:53.024506+00	2026-07-28 03:48:53.024506+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
8eebdb4a-5f13-4b7a-ba24-48a0137bd401	e61d21e3-8cab-47e3-bd28-341a78a457a9	302	3	\N	t	2026-07-28 03:48:53.023697+00	2026-07-28 03:48:53.023697+00	f63b2806-634c-41a3-89db-43ffab39e38e
9ffbf6d5-9a11-47f4-85b5-82b09ec6123d	e61d21e3-8cab-47e3-bd28-341a78a457a9	403	4	\N	t	2026-07-28 03:48:53.024546+00	2026-07-28 03:48:53.024546+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
a5887023-4d2b-492d-880e-10f372cfe89a	e61d21e3-8cab-47e3-bd28-341a78a457a9	101	1	\N	t	2026-07-28 03:48:53.020247+00	2026-07-28 03:48:53.020247+00	f63b2806-634c-41a3-89db-43ffab39e38e
a9c737ce-70ec-4113-abbe-5141792f3613	e61d21e3-8cab-47e3-bd28-341a78a457a9	406	4	\N	t	2026-07-28 03:48:53.024595+00	2026-07-28 03:48:53.024595+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
ac6ac355-3e50-4bc5-adeb-da1e82050171	e61d21e3-8cab-47e3-bd28-341a78a457a9	504	5	\N	t	2026-07-28 03:48:53.023998+00	2026-07-28 03:48:53.023998+00	f63b2806-634c-41a3-89db-43ffab39e38e
afd4fa4e-fca6-48a6-9928-f41d4ac1a576	e61d21e3-8cab-47e3-bd28-341a78a457a9	204	2	\N	t	2026-07-28 03:48:53.023648+00	2026-07-28 03:48:53.023648+00	f63b2806-634c-41a3-89db-43ffab39e38e
b6b55a73-8068-494b-9fdd-6776d60bf2e8	e61d21e3-8cab-47e3-bd28-341a78a457a9	205	2	\N	t	2026-07-28 03:48:53.024391+00	2026-07-28 03:48:53.024391+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
b73db19b-4f7b-4844-8320-5a89cdfff06f	e61d21e3-8cab-47e3-bd28-341a78a457a9	402	4	\N	t	2026-07-28 03:48:53.023821+00	2026-07-28 03:48:53.023821+00	f63b2806-634c-41a3-89db-43ffab39e38e
b9f44cfa-c90c-44ff-b1f4-aaf69d8f033f	e61d21e3-8cab-47e3-bd28-341a78a457a9	501	5	\N	t	2026-07-28 03:48:53.023917+00	2026-07-28 03:48:53.023917+00	f63b2806-634c-41a3-89db-43ffab39e38e
bdef08cf-3f99-4a69-a1e6-6aa01bf99a37	e61d21e3-8cab-47e3-bd28-341a78a457a9	405	4	\N	t	2026-07-28 03:48:53.02458+00	2026-07-28 03:48:53.02458+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
bed93c9b-39fd-40a7-b5eb-8d2a129c6627	e61d21e3-8cab-47e3-bd28-341a78a457a9	101	1	\N	t	2026-07-28 03:48:53.024122+00	2026-07-28 03:48:53.024122+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
c23abc8d-ea20-437a-9a5b-6bfa6ce36129	e61d21e3-8cab-47e3-bd28-341a78a457a9	305	3	\N	t	2026-07-28 03:48:53.02449+00	2026-07-28 03:48:53.02449+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
c7ccdbc9-1870-4000-9065-bbb992fb0f1d	e61d21e3-8cab-47e3-bd28-341a78a457a9	203	2	\N	t	2026-07-28 03:48:53.024361+00	2026-07-28 03:48:53.024361+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
d81368c2-879c-4f4c-9d21-18c6e538b59f	e61d21e3-8cab-47e3-bd28-341a78a457a9	102	1	\N	t	2026-07-28 03:48:53.0235+00	2026-07-28 03:48:53.0235+00	f63b2806-634c-41a3-89db-43ffab39e38e
db52d834-8ee8-4211-8337-bb1630726b33	e61d21e3-8cab-47e3-bd28-341a78a457a9	104	1	\N	t	2026-07-28 03:48:53.023584+00	2026-07-28 03:48:53.023584+00	f63b2806-634c-41a3-89db-43ffab39e38e
de2ee7b3-5ca8-44b5-9f07-cfb627587c52	e61d21e3-8cab-47e3-bd28-341a78a457a9	602	6	\N	t	2026-07-28 03:48:53.024048+00	2026-07-28 03:48:53.024048+00	f63b2806-634c-41a3-89db-43ffab39e38e
ee2d614b-f182-45ee-87ac-ac36b2ff4184	e61d21e3-8cab-47e3-bd28-341a78a457a9	206	2	\N	t	2026-07-28 03:48:53.024405+00	2026-07-28 03:48:53.024405+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
f6fcb391-3580-4f7d-80cf-80327e56968d	e61d21e3-8cab-47e3-bd28-341a78a457a9	103	1	\N	t	2026-07-28 03:48:53.024183+00	2026-07-28 03:48:53.024183+00	848971b3-57b5-4f9f-a30d-910e315a8e7b
f90f28e9-1093-4e0c-8344-a2c3f5e47963	e61d21e3-8cab-47e3-bd28-341a78a457a9	103	1	\N	t	2026-07-28 03:48:53.023564+00	2026-07-28 03:48:53.023564+00	f63b2806-634c-41a3-89db-43ffab39e38e
faeada0e-00cf-433d-ab1e-aa6227f021f5	e61d21e3-8cab-47e3-bd28-341a78a457a9	403	4	\N	t	2026-07-28 03:48:53.02385+00	2026-07-28 03:48:53.02385+00	f63b2806-634c-41a3-89db-43ffab39e38e
fe4ed097-0782-471e-b070-a3f37e11a4e7	e61d21e3-8cab-47e3-bd28-341a78a457a9	404	4	\N	t	2026-07-28 03:48:53.023875+00	2026-07-28 03:48:53.023875+00	f63b2806-634c-41a3-89db-43ffab39e38e
\.


--
-- Data for Name: users; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.users (id, full_name, is_active, created_at, updated_at, user_name, normalized_user_name, email, normalized_email, email_confirmed, password_hash, security_stamp, concurrency_stamp, phone_number, phone_number_confirmed, two_factor_enabled, lockout_end, lockout_enabled, access_failed_count, active_management_condominium_id, uses_consolidated_management_scope, address, city, cnpj, cpf, state, last_login_at, must_change_password, password_changed_at, receive_whatsapp_updates) FROM stdin;
10a2a96c-8f5f-4a05-a750-6a5a928aebea	Tati	t	2026-07-16 20:47:55.285103+00	2026-07-16 20:47:55.285103+00	tati@teste	TATI@TESTE	tati@teste	TATI@TESTE	f	AQAAAAIAAYagAAAAELVpWTXkcimucSp50MKK51bPBBXWsNbQmMqilqQerhVfPIZEOfiN7aGklG+hxGzjNw==	3LMWIVVW3HH53ZFXDCEJQG4FRZOGICXE	40786def-5e68-4ba6-9196-f74f0407072c	44997562161	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
4e373240-3166-42a4-89a1-2e2ef41ec63c	Test User	t	2026-07-17 14:10:18.209881+00	2026-07-17 14:10:18.209881+00	testuser@example.com	TESTUSER@EXAMPLE.COM	testuser@example.com	TESTUSER@EXAMPLE.COM	f	AQAAAAIAAYagAAAAEF9zPquDW364iKglbK5Yy8rNSXbpvIFSiUgB0meoFUEQZCLY3ufCjUXxFDn17qm/uA==	ZM4VD4VD7RO4JLL6F3BITZLUKG5RW4J3	f4487dba-1542-4ce3-84de-7faf578264a5	123456789	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
c1ba77a2-b36c-4987-9ae7-6b7e8b9bcd70	Thiago	t	2026-07-26 03:28:34.046658+00	2026-07-26 03:28:34.046658+00	condominios@dimarp.com	CONDOMINIOS@DIMARP.COM	condominios@dimarp.com	CONDOMINIOS@DIMARP.COM	f	AQAAAAIAAYagAAAAEAoMGt4TfXQGG8x9uYbFB+w8HSvxMIhbEnss+Pf8ZR0oiflHVlT3lFw0UzWy7W5X+g==	COP5NBIMSYF6TH6I3MFO3FDBGFPAS3FE	10ad2ba6-d709-4811-8fc5-2912aa7ef7fe	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
eb8a1524-8a79-4ecb-b761-d8d2a68817ba	Plínio Prudêncio	t	2026-07-20 15:26:58.760775+00	2026-07-20 15:26:58.760775+00	plinio@sindico.com	PLINIO@SINDICO.COM	plinio@sindico.com	PLINIO@SINDICO.COM	f	AQAAAAIAAYagAAAAED3SmQfH9RKX2AQcovo8bj0wP5vI6xJznB6eeLH/1paSX6YZLTo8KojXaPhe8XAaGA==	TZAXQXWBZTCRVMDO2RVZMIXJVB4K6UDR	fe4a0792-a0c4-41fa-9888-45ee10c686c9	44999999999	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
9fc09869-4ffd-4d49-83c6-2aff3d89e2a5	Pessoa do inga	t	2026-07-20 17:57:44.061394+00	2026-07-20 17:57:44.061394+00	pessoa@inga.com	PESSOA@INGA.COM	pessoa@inga.com	PESSOA@INGA.COM	f	AQAAAAIAAYagAAAAEBxESSfXOI4tjyAK0G3xfARehtKE2g3zRUSskvNq3B7Kpi0oZBEXnoQ6/M9F+5zaxw==	UUBXLOO3JQS36E7LEEWA7COIHD4H4CU6	7e20245f-8aa2-40a3-b50c-82d8900502a6	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
8e3ad79b-fba4-462e-a913-9cd31cfdfe1f	Pessoa 2	t	2026-07-20 17:30:18.49374+00	2026-07-20 17:30:18.49374+00	pessoa@2.com	PESSOA@2.COM	pessoa@2.com	PESSOA@2.COM	f	AQAAAAIAAYagAAAAEFMt08tju2YdTNZeiznnDuT3HxnykeE+JwmlgMW8Ca/HtbKK0udYGzitKhtDPEXiwg==	BJS62LY6TFVOUK6DNNNBH7Q5U5DIS3FQ	1cdf49ad-b21d-451b-9fd3-3073e0222b9c	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
1bfdc06d-9f08-400e-a3da-a227d747a5fa	teste	t	2026-07-20 17:30:53.614349+00	2026-07-20 17:30:53.614349+00	teste@testeee	TESTE@TESTEEE	teste@testeee	TESTE@TESTEEE	f	AQAAAAIAAYagAAAAEATkH+NVvikBiUx5oQOn6Ct5BWekTFlp2P0wMuxjj1S+vSZ27iNQc2sAKadbfHQyDQ==	ROKYKEVY3TXSIVGYNHSR4YLGUEDDWI7V	387f3a2c-7947-4702-8e62-4cbd862930bc	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
5c0e37d6-e40c-40e9-87d1-6f1f34a376ce	Tatiana Custódio Beltrã	t	2026-07-28 02:47:43.51698+00	2026-07-28 04:50:25.854102+00	truelisandropb@gmail.com	TRUELISANDROPB@GMAIL.COM	truelisandropb@gmail.com	TRUELISANDROPB@GMAIL.COM	f	AQAAAAIAAYagAAAAEGZvaizss2aINlxsJTctJ7MKokOP9VpkvNEjibvEvZ0GLU011OZM3Yl7nIuhgwjOIQ==	XFM3IZWWO7N47Z6FC77BDW5A727XSHWU	b3b01e12-02d1-41cd-b3e4-3c7c89cf6214	44997562161	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	2026-07-28 03:57:47.07219+00	f	2026-07-28 03:10:58.027742+00	f
d2b48f78-26ff-452b-b864-8eedbf5793b2	Pessoa1	t	2026-07-20 16:39:57.409749+00	2026-07-20 16:39:57.409749+00	pessoa@1.com	PESSOA@1.COM	pessoa@1.com	PESSOA@1.COM	f	AQAAAAIAAYagAAAAEP4SLHWRB4oSAporUW7wZRBXaveRxgQob98yG+tyfjcDc0DS6NmVfqvaGAAusrRSlQ==	WS7F3LJQCN6SASCLJ4QK7EE4XHBA7KPM	b56becbf-ac56-4b0f-a7cb-7694a6553bf1	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
1e80376a-c6b4-445b-8a6f-0ced2d85f8c2	teste de meail	t	2026-07-20 17:31:10.74072+00	2026-07-20 17:31:10.74072+00	conta@conta	CONTA@CONTA	conta@conta	CONTA@CONTA	f	AQAAAAIAAYagAAAAELJnQheib4avTvSXNNaemIlWVrn6znqbwbPInyo4Plmp3PVxAkUhMU5eDJECbJLx6A==	WFIYPTU6G7PIRYHOTBNNEGINLIJZLF22	25353e95-b578-4e1c-985f-eba14faa1283	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
7fbf6731-8774-4bc8-acfe-b386a54a6bf8	Pessoa1	t	2026-07-20 16:41:31.948387+00	2026-07-20 16:41:31.948387+00	pessoa1@teste.com	PESSOA1@TESTE.COM	pessoa1@teste.com	PESSOA1@TESTE.COM	f	AQAAAAIAAYagAAAAEJhdae4VX1QU/NwsXE5Cn0AQbZO9cPkjqYV6fDnAfB/yC8K0gRvIWWW7sPsq1gPGdA==	D3E73RDKHGYXQ23G2TW23J6GBKJSJHAG	2cbd9158-1774-4b07-ac8d-07ebbcb70716	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
3526eabc-0c28-40f4-8ae4-24b1fcdcae51	Pessoa do central	t	2026-07-20 17:55:18.296542+00	2026-07-20 17:55:18.296542+00	centr@pessoa.com	CENTR@PESSOA.COM	centr@pessoa.com	CENTR@PESSOA.COM	f	AQAAAAIAAYagAAAAENgiilsAPY2qMVRpCAd2YHaqeeWqukQAt4TKL/wT7goSw1E2YFS04OYfrbsH/4W8oQ==	RJ5CNRASJS6UDKG3IFJXH63JIXN7HYIZ	0b4184ad-5e6c-42f8-8416-8b0edf46c6be	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
368e5e05-df65-4a38-8c74-f29086b6029a	Pessoa 2 do central	t	2026-07-20 17:56:18.475372+00	2026-07-20 17:56:18.475372+00	centr@2.com	CENTR@2.COM	centr@2.com	CENTR@2.COM	f	AQAAAAIAAYagAAAAEJ0M4QJxe6WLXlRmijk8cDc5weAO/IuplDWjhb4xqdSWdmBsZlSBusgKEXl9zZSZdg==	ZWWRWHS2LBUOUWUCECDBTA2MLVCXUJXK	b22813e2-d8c6-4b2b-920c-82a8783f8b98	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
cf8cdc65-08b5-4631-b2f8-784c27a5dc35	Usuário Teste	t	2026-07-16 12:46:04.905143+00	2026-07-16 12:46:04.905143+00	usuario@example.com	USUARIO@EXAMPLE.COM	usuario@example.com	USUARIO@EXAMPLE.COM	f	AQAAAAIAAYagAAAAELoSlVIYA/hR3dj5GSt8ocYuny/VXYimxGapNmPNbifp/Aus3wZ/RxiducmqJMP74Q==	AR5DJWE7BJ7TL62FWY6BUWXIMPOXM52B	f48f9b9a-a680-4074-a80e-bcb7e716a68c	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
65c6ab7c-17f4-4229-ad43-7ea1ba1303c5	Marina Oliveira	t	2026-07-16 18:06:05.895147+00	2026-07-16 18:06:05.895147+00	morador@condolink.local	MORADOR@CONDOLINK.LOCAL	morador@condolink.local	MORADOR@CONDOLINK.LOCAL	f	AQAAAAIAAYagAAAAEIk6hzcDDHTPe3GrBLAk+AhPxOqSr+JD07pc6Pp+rx7HNMn8usU0JjyGa84LDr1i2g==	3G7EAXREGJ6VL7OH5P4PARCIDAXP5PNK	d8db47a8-cced-42db-aa1c-b5f1a624d23a	43988887777	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
c08f02ee-2a03-4214-94a5-c5956fbe0dff	Carlos Almeida	t	2026-07-16 19:43:55.45116+00	2026-07-16 19:43:55.45116+00	carlos@condolink.local	CARLOS@CONDOLINK.LOCAL	carlos@condolink.local	CARLOS@CONDOLINK.LOCAL	f	AQAAAAIAAYagAAAAEM7WMfAlTudPMynJMTNMH3Gub+uyFuZyG1Re9AJLsDBywcmQyS3BeJRWTWCg3p0oqQ==	5ZWG4MBW7QNVS6JAJFLJLR7UCN5G2CCJ	de2be1ca-4843-4a91-a156-be6fd94e39a3	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
ee187bc0-c4ba-4a99-bb1b-f25551c179ed	Guilherme	t	2026-07-26 04:09:33.032863+00	2026-07-26 04:09:33.032863+00	guilherme@dimarp.com	GUILHERME@DIMARP.COM	guilherme@dimarp.com	GUILHERME@DIMARP.COM	f	AQAAAAIAAYagAAAAEAH53hKCGXL7SH4WgHvplTl30PO5F7RQPKb0oQcpCLwsArzfKyuYPzqp9QBQMYQ5pw==	WTPF4OJC2XWISOIO6NERVQ53HZE4BLVC	6445d3b0-b58e-4a39-9338-fe337417dbe2	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
4e48eb75-23a3-458c-b1c7-ab9a44a5e786	Plinio Prudêncio	t	2026-07-20 15:50:54.965189+00	2026-07-27 15:58:31.04981+00	plinio@macio.com	PLINIO@MACIO.COM	plinio@macio.com	PLINIO@MACIO.COM	f	AQAAAAIAAYagAAAAEBrLvs9HeMgr8k5rO0QbR5mAOtH/pABJXZZS1Sn4eV3QyhIJokSmhU6aflFYjHaDrg==	JMSUGGR4SNKKJJU7QIYGYWH5GL2PNSXM	2de19aae-e024-468a-8ddf-b5230c7bbd5f	44999999999	f	f	\N	t	0	d721c30f-1417-465e-91fc-930eb53a2cd8	\N	\N	\N	\N	\N	\N	\N	f	\N	f
ae947f34-226d-45d3-8dd8-3778b237d5bf	lisandro	t	2026-07-26 02:11:22.83272+00	2026-07-27 13:16:21.275498+00	lisandro@lisandro.com	LISANDRO@LISANDRO.COM	lisandro@lisandro.com	LISANDRO@LISANDRO.COM	f	AQAAAAIAAYagAAAAEB+CZ+Y02rjk3Zh4o9KLSLsxkGE2NjH2hUVvMte4mx0z3kIJ7GKT6MzcAFeW52vuRQ==	Z45PESKG7FNDE2BTAQN5XM5W3TYYA5ZH	ccb050c3-7553-42b9-be11-1e051c0aa3a4	44997562161	f	f	\N	t	0	\N	\N	Avenida Monteiro Lobato, 1530	Maringá	\N	\N	\N	\N	f	\N	f
2e38f0b9-18a0-446c-9ad7-009457fd86d6	Vitor almeida	t	2026-07-27 12:25:33.359516+00	2026-07-27 12:25:33.359516+00	vitor@almeida.com	VITOR@ALMEIDA.COM	vitor@almeida.com	VITOR@ALMEIDA.COM	f	AQAAAAIAAYagAAAAEHLWrp7AUnKnDcy06zj3PXNih9BwmK2xlSglHyhgiy5VL/xQoNUZXtGEG8v/WeNP/Q==	O6IXGJNFVLIZJVZJ6QCXBCCPTCPSSFTD	b704abea-1254-4175-8076-0e43df504024	\N	f	f	\N	t	0	\N	\N	\N	\N	\N	\N	\N	\N	f	\N	f
c63718e2-d2a6-4822-ac99-2e05d0912be4	Lisandro Beltrã	t	2026-07-16 12:46:04.540854+00	2026-07-28 12:46:10.128428+00	lisandro@example.com	LISANDRO@EXAMPLE.COM	lisandro@example.com	LISANDRO@EXAMPLE.COM	f	AQAAAAIAAYagAAAAEFEIG9SFJTa7QPP6aig8R1KHXQSerDsmzdcB0AljZLANy/siy6NCB+gTh6jglvi2Vg==	WKOVM6QPPTCIQ2I23TINJQTRXB5HD7GA	ded46199-6070-4265-933a-ca02a7d67164	43999999999	f	f	\N	t	0	e61d21e3-8cab-47e3-bd28-341a78a457a9	\N	\N	\N	\N	\N	\N	2026-07-28 12:46:10.128428+00	f	\N	f
7bcbd08e-4607-4cd0-a9b6-d492002c65a0	Thiago	t	2026-07-27 12:23:39.805644+00	2026-07-27 15:58:31.047516+00	thiago@thiago.com	THIAGO@THIAGO.COM	thiago@thiago.com	THIAGO@THIAGO.COM	f	AQAAAAIAAYagAAAAEFI1NKw1jswKyrXZDk5IYwDGXLH8wI2ykanWLNAEq+QNULBYm4IE856neSN4e+a3Jg==	2VB2GIBFNOBNXNCMSXPKB3WGVS447E7H	391de106-c640-4e93-b498-ce0e65d6c312	\N	f	f	\N	t	0	59d9844b-2207-4884-ac68-43357392b2c3	\N	\N	\N	\N	\N	\N	\N	f	\N	f
\.


--
-- Data for Name: whatsapp_draft_attachments; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.whatsapp_draft_attachments (id, session_id, external_media_id, original_file_name, storage_key, content_type, file_size, created_at) FROM stdin;
\.


--
-- Data for Name: whatsapp_inbound_messages; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.whatsapp_inbound_messages (id, external_message_id, phone_number, message_type, text, provider_timestamp, received_at, processed_at, identified_user_id, processing_result) FROM stdin;
\.


--
-- Data for Name: whatsapp_outbound_messages; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.whatsapp_outbound_messages (id, request_id, request_message_id, user_id, condominium_id, destination_phone, notification_type, send_mode, template_name, template_language, content, external_message_id, status, attempt_count, manual_retry_count, next_attempt_at, created_at, sent_at, delivered_at, read_at, failed_at, last_error_code, last_error_description, idempotency_key, version) FROM stdin;
\.


--
-- Data for Name: whatsapp_sessions; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.whatsapp_sessions (id, phone_number, user_id, condominium_id, unit_id, request_id, state, previous_state, last_interaction_at, expires_at, version, category_id, draft_description, page) FROM stdin;
\.


--
-- Name: AspNetRoleClaims_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public."AspNetRoleClaims_Id_seq"', 1, false);


--
-- Name: AspNetUserClaims_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public."AspNetUserClaims_Id_seq"', 1, false);


--
-- Name: AspNetRoleClaims PK_AspNetRoleClaims; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetRoleClaims"
    ADD CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY ("Id");


--
-- Name: AspNetRoles PK_AspNetRoles; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetRoles"
    ADD CONSTRAINT "PK_AspNetRoles" PRIMARY KEY ("Id");


--
-- Name: AspNetUserClaims PK_AspNetUserClaims; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserClaims"
    ADD CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY ("Id");


--
-- Name: AspNetUserLogins PK_AspNetUserLogins; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserLogins"
    ADD CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey");


--
-- Name: AspNetUserRoles PK_AspNetUserRoles; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserRoles"
    ADD CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId");


--
-- Name: AspNetUserTokens PK_AspNetUserTokens; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserTokens"
    ADD CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: categories PK_categories; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT "PK_categories" PRIMARY KEY (id);


--
-- Name: condominium_blocks PK_condominium_blocks; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.condominium_blocks
    ADD CONSTRAINT "PK_condominium_blocks" PRIMARY KEY (id);


--
-- Name: condominium_membership_roles PK_condominium_membership_roles; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.condominium_membership_roles
    ADD CONSTRAINT "PK_condominium_membership_roles" PRIMARY KEY (id);


--
-- Name: condominium_memberships PK_condominium_memberships; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.condominium_memberships
    ADD CONSTRAINT "PK_condominium_memberships" PRIMARY KEY (id);


--
-- Name: condominiums PK_condominiums; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.condominiums
    ADD CONSTRAINT "PK_condominiums" PRIMARY KEY (id);


--
-- Name: management_companies PK_management_companies; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.management_companies
    ADD CONSTRAINT "PK_management_companies" PRIMARY KEY (id);


--
-- Name: management_company_employees PK_management_company_employees; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.management_company_employees
    ADD CONSTRAINT "PK_management_company_employees" PRIMARY KEY (id);


--
-- Name: management_company_request_categories PK_management_company_request_categories; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.management_company_request_categories
    ADD CONSTRAINT "PK_management_company_request_categories" PRIMARY KEY (id);


--
-- Name: notifications PK_notifications; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notifications
    ADD CONSTRAINT "PK_notifications" PRIMARY KEY (id);


--
-- Name: request_attachments PK_request_attachments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_attachments
    ADD CONSTRAINT "PK_request_attachments" PRIMARY KEY (id);


--
-- Name: request_messages PK_request_messages; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_messages
    ADD CONSTRAINT "PK_request_messages" PRIMARY KEY (id);


--
-- Name: request_status_history PK_request_status_history; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_status_history
    ADD CONSTRAINT "PK_request_status_history" PRIMARY KEY (id);


--
-- Name: requests PK_requests; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.requests
    ADD CONSTRAINT "PK_requests" PRIMARY KEY (id);


--
-- Name: unit_memberships PK_unit_memberships; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.unit_memberships
    ADD CONSTRAINT "PK_unit_memberships" PRIMARY KEY (id);


--
-- Name: units PK_units; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.units
    ADD CONSTRAINT "PK_units" PRIMARY KEY (id);


--
-- Name: users PK_users; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT "PK_users" PRIMARY KEY (id);


--
-- Name: whatsapp_draft_attachments PK_whatsapp_draft_attachments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_draft_attachments
    ADD CONSTRAINT "PK_whatsapp_draft_attachments" PRIMARY KEY (id);


--
-- Name: whatsapp_inbound_messages PK_whatsapp_inbound_messages; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_inbound_messages
    ADD CONSTRAINT "PK_whatsapp_inbound_messages" PRIMARY KEY (id);


--
-- Name: whatsapp_outbound_messages PK_whatsapp_outbound_messages; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_outbound_messages
    ADD CONSTRAINT "PK_whatsapp_outbound_messages" PRIMARY KEY (id);


--
-- Name: whatsapp_sessions PK_whatsapp_sessions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_sessions
    ADD CONSTRAINT "PK_whatsapp_sessions" PRIMARY KEY (id);


--
-- Name: IX_AspNetRoleClaims_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON public."AspNetRoleClaims" USING btree ("RoleId");


--
-- Name: IX_AspNetUserClaims_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AspNetUserClaims_UserId" ON public."AspNetUserClaims" USING btree ("UserId");


--
-- Name: IX_AspNetUserLogins_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AspNetUserLogins_UserId" ON public."AspNetUserLogins" USING btree ("UserId");


--
-- Name: IX_AspNetUserRoles_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AspNetUserRoles_RoleId" ON public."AspNetUserRoles" USING btree ("RoleId");


--
-- Name: IX_condominium_memberships_condominium_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_condominium_memberships_condominium_id" ON public.condominium_memberships USING btree (condominium_id);


--
-- Name: IX_notifications_condominium_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_notifications_condominium_id" ON public.notifications USING btree (condominium_id);


--
-- Name: IX_notifications_recipient_user_id_condominium_id_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_notifications_recipient_user_id_condominium_id_created_at" ON public.notifications USING btree (recipient_user_id, condominium_id, created_at);


--
-- Name: IX_notifications_recipient_user_id_read_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_notifications_recipient_user_id_read_at" ON public.notifications USING btree (recipient_user_id, read_at);


--
-- Name: IX_notifications_request_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_notifications_request_id" ON public.notifications USING btree (request_id);


--
-- Name: IX_request_attachments_request_id_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_request_attachments_request_id_created_at" ON public.request_attachments USING btree (request_id, created_at);


--
-- Name: IX_request_attachments_request_message_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_request_attachments_request_message_id" ON public.request_attachments USING btree (request_message_id);


--
-- Name: IX_request_attachments_uploaded_by_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_request_attachments_uploaded_by_user_id" ON public.request_attachments USING btree (uploaded_by_user_id);


--
-- Name: IX_request_messages_author_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_request_messages_author_user_id" ON public.request_messages USING btree (author_user_id);


--
-- Name: IX_request_messages_request_id_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_request_messages_request_id_created_at" ON public.request_messages USING btree (request_id, created_at);


--
-- Name: IX_request_status_history_changed_by_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_request_status_history_changed_by_user_id" ON public.request_status_history USING btree (changed_by_user_id);


--
-- Name: IX_request_status_history_request_id_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_request_status_history_request_id_created_at" ON public.request_status_history USING btree (request_id, created_at);


--
-- Name: IX_requests_author_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_requests_author_user_id" ON public.requests USING btree (author_user_id);


--
-- Name: IX_requests_category_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_requests_category_id" ON public.requests USING btree (category_id);


--
-- Name: IX_requests_condominium_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_requests_condominium_id" ON public.requests USING btree (condominium_id);


--
-- Name: IX_requests_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_requests_created_at" ON public.requests USING btree (created_at);


--
-- Name: IX_requests_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_requests_status" ON public.requests USING btree (status);


--
-- Name: IX_requests_target_unit_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_requests_target_unit_id" ON public.requests USING btree (target_unit_id);


--
-- Name: IX_unit_memberships_unit_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_unit_memberships_unit_id" ON public.unit_memberships USING btree (unit_id);


--
-- Name: IX_whatsapp_draft_attachments_session_id_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_draft_attachments_session_id_created_at" ON public.whatsapp_draft_attachments USING btree (session_id, created_at);


--
-- Name: IX_whatsapp_inbound_messages_identified_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_inbound_messages_identified_user_id" ON public.whatsapp_inbound_messages USING btree (identified_user_id);


--
-- Name: IX_whatsapp_outbound_messages_condominium_id_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_outbound_messages_condominium_id_created_at" ON public.whatsapp_outbound_messages USING btree (condominium_id, created_at);


--
-- Name: IX_whatsapp_outbound_messages_request_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_outbound_messages_request_id" ON public.whatsapp_outbound_messages USING btree (request_id);


--
-- Name: IX_whatsapp_outbound_messages_request_message_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_outbound_messages_request_message_id" ON public.whatsapp_outbound_messages USING btree (request_message_id);


--
-- Name: IX_whatsapp_outbound_messages_status_next_attempt_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_outbound_messages_status_next_attempt_at" ON public.whatsapp_outbound_messages USING btree (status, next_attempt_at);


--
-- Name: IX_whatsapp_outbound_messages_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_outbound_messages_user_id" ON public.whatsapp_outbound_messages USING btree (user_id);


--
-- Name: IX_whatsapp_sessions_category_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_sessions_category_id" ON public.whatsapp_sessions USING btree (category_id);


--
-- Name: IX_whatsapp_sessions_condominium_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_sessions_condominium_id" ON public.whatsapp_sessions USING btree (condominium_id);


--
-- Name: IX_whatsapp_sessions_request_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_sessions_request_id" ON public.whatsapp_sessions USING btree (request_id);


--
-- Name: IX_whatsapp_sessions_unit_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_sessions_unit_id" ON public.whatsapp_sessions USING btree (unit_id);


--
-- Name: IX_whatsapp_sessions_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_whatsapp_sessions_user_id" ON public.whatsapp_sessions USING btree (user_id);


--
-- Name: RoleNameIndex; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "RoleNameIndex" ON public."AspNetRoles" USING btree ("NormalizedName");


--
-- Name: UserNameIndex; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UserNameIndex" ON public.users USING btree (normalized_user_name);


--
-- Name: ix_condominiums_management_company_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_condominiums_management_company_id ON public.condominiums USING btree (management_company_id);


--
-- Name: ix_management_company_employees_management_company_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_management_company_employees_management_company_id ON public.management_company_employees USING btree (management_company_id);


--
-- Name: ux_categories_condominium_normalized_name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_categories_condominium_normalized_name ON public.categories USING btree (condominium_id, normalized_name);


--
-- Name: ux_condominium_blocks_condominium_identifier; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_condominium_blocks_condominium_identifier ON public.condominium_blocks USING btree (condominium_id, identifier);


--
-- Name: ux_condominium_membership_roles_membership_role; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_condominium_membership_roles_membership_role ON public.condominium_membership_roles USING btree (condominium_membership_id, role);


--
-- Name: ux_condominium_memberships_user_condominium; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_condominium_memberships_user_condominium ON public.condominium_memberships USING btree (user_id, condominium_id);


--
-- Name: ux_condominiums_cnpj; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_condominiums_cnpj ON public.condominiums USING btree (cnpj) WHERE (cnpj IS NOT NULL);


--
-- Name: ux_management_companies_cnpj; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_management_companies_cnpj ON public.management_companies USING btree (cnpj) WHERE (cnpj IS NOT NULL);


--
-- Name: ux_management_companies_email; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_management_companies_email ON public.management_companies USING btree (email) WHERE (email IS NOT NULL);


--
-- Name: ux_management_company_employees_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_management_company_employees_user_id ON public.management_company_employees USING btree (user_id);


--
-- Name: ux_management_company_request_categories_company_normalized_nam; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_management_company_request_categories_company_normalized_nam ON public.management_company_request_categories USING btree (management_company_id, normalized_name);


--
-- Name: ux_unit_memberships_user_unit_relationship; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_unit_memberships_user_unit_relationship ON public.unit_memberships USING btree (user_id, unit_id, relationship_type);


--
-- Name: ux_units_block_identifier; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_units_block_identifier ON public.units USING btree (block_id, identifier) WHERE (block_id IS NOT NULL);


--
-- Name: ux_units_condominium_identifier_without_block_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_units_condominium_identifier_without_block_id ON public.units USING btree (condominium_id, identifier) WHERE (block_id IS NULL);


--
-- Name: ux_users_manager_cnpj; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_users_manager_cnpj ON public.users USING btree (cnpj) WHERE (cnpj IS NOT NULL);


--
-- Name: ux_users_manager_cpf; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_users_manager_cpf ON public.users USING btree (cpf) WHERE (cpf IS NOT NULL);


--
-- Name: ux_users_normalized_email; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_users_normalized_email ON public.users USING btree (normalized_email);


--
-- Name: ux_whatsapp_draft_attachments_external_media_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_whatsapp_draft_attachments_external_media_id ON public.whatsapp_draft_attachments USING btree (external_media_id);


--
-- Name: ux_whatsapp_inbound_messages_external_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_whatsapp_inbound_messages_external_id ON public.whatsapp_inbound_messages USING btree (external_message_id);


--
-- Name: ux_whatsapp_outbound_external_message_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_whatsapp_outbound_external_message_id ON public.whatsapp_outbound_messages USING btree (external_message_id) WHERE (external_message_id IS NOT NULL);


--
-- Name: ux_whatsapp_outbound_idempotency_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_whatsapp_outbound_idempotency_key ON public.whatsapp_outbound_messages USING btree (idempotency_key);


--
-- Name: ux_whatsapp_sessions_phone_number; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_whatsapp_sessions_phone_number ON public.whatsapp_sessions USING btree (phone_number);


--
-- Name: AspNetRoleClaims FK_AspNetRoleClaims_AspNetRoles_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetRoleClaims"
    ADD CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES public."AspNetRoles"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserClaims FK_AspNetUserClaims_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserClaims"
    ADD CONSTRAINT "FK_AspNetUserClaims_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users(id) ON DELETE CASCADE;


--
-- Name: AspNetUserLogins FK_AspNetUserLogins_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserLogins"
    ADD CONSTRAINT "FK_AspNetUserLogins_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users(id) ON DELETE CASCADE;


--
-- Name: AspNetUserRoles FK_AspNetUserRoles_AspNetRoles_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserRoles"
    ADD CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES public."AspNetRoles"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserRoles FK_AspNetUserRoles_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserRoles"
    ADD CONSTRAINT "FK_AspNetUserRoles_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users(id) ON DELETE CASCADE;


--
-- Name: AspNetUserTokens FK_AspNetUserTokens_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserTokens"
    ADD CONSTRAINT "FK_AspNetUserTokens_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users(id) ON DELETE CASCADE;


--
-- Name: categories FK_categories_condominiums_condominium_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT "FK_categories_condominiums_condominium_id" FOREIGN KEY (condominium_id) REFERENCES public.condominiums(id) ON DELETE RESTRICT;


--
-- Name: condominium_blocks FK_condominium_blocks_condominiums_condominium_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.condominium_blocks
    ADD CONSTRAINT "FK_condominium_blocks_condominiums_condominium_id" FOREIGN KEY (condominium_id) REFERENCES public.condominiums(id) ON DELETE RESTRICT;


--
-- Name: condominium_membership_roles FK_condominium_membership_roles_condominium_memberships_condom~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.condominium_membership_roles
    ADD CONSTRAINT "FK_condominium_membership_roles_condominium_memberships_condom~" FOREIGN KEY (condominium_membership_id) REFERENCES public.condominium_memberships(id) ON DELETE RESTRICT;


--
-- Name: condominium_memberships FK_condominium_memberships_condominiums_condominium_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.condominium_memberships
    ADD CONSTRAINT "FK_condominium_memberships_condominiums_condominium_id" FOREIGN KEY (condominium_id) REFERENCES public.condominiums(id) ON DELETE RESTRICT;


--
-- Name: condominium_memberships FK_condominium_memberships_users_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.condominium_memberships
    ADD CONSTRAINT "FK_condominium_memberships_users_user_id" FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: condominiums FK_condominiums_management_companies_management_company_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.condominiums
    ADD CONSTRAINT "FK_condominiums_management_companies_management_company_id" FOREIGN KEY (management_company_id) REFERENCES public.management_companies(id) ON DELETE SET NULL;


--
-- Name: management_company_employees FK_management_company_employees_management_companies_managemen~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.management_company_employees
    ADD CONSTRAINT "FK_management_company_employees_management_companies_managemen~" FOREIGN KEY (management_company_id) REFERENCES public.management_companies(id) ON DELETE RESTRICT;


--
-- Name: management_company_employees FK_management_company_employees_users_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.management_company_employees
    ADD CONSTRAINT "FK_management_company_employees_users_user_id" FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;


--
-- Name: management_company_request_categories FK_management_company_request_categories_management_companies_~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.management_company_request_categories
    ADD CONSTRAINT "FK_management_company_request_categories_management_companies_~" FOREIGN KEY (management_company_id) REFERENCES public.management_companies(id) ON DELETE RESTRICT;


--
-- Name: notifications FK_notifications_condominiums_condominium_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notifications
    ADD CONSTRAINT "FK_notifications_condominiums_condominium_id" FOREIGN KEY (condominium_id) REFERENCES public.condominiums(id) ON DELETE RESTRICT;


--
-- Name: notifications FK_notifications_requests_request_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notifications
    ADD CONSTRAINT "FK_notifications_requests_request_id" FOREIGN KEY (request_id) REFERENCES public.requests(id) ON DELETE CASCADE;


--
-- Name: notifications FK_notifications_users_recipient_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notifications
    ADD CONSTRAINT "FK_notifications_users_recipient_user_id" FOREIGN KEY (recipient_user_id) REFERENCES public.users(id) ON DELETE CASCADE;


--
-- Name: request_attachments FK_request_attachments_request_messages_request_message_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_attachments
    ADD CONSTRAINT "FK_request_attachments_request_messages_request_message_id" FOREIGN KEY (request_message_id) REFERENCES public.request_messages(id) ON DELETE RESTRICT;


--
-- Name: request_attachments FK_request_attachments_requests_request_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_attachments
    ADD CONSTRAINT "FK_request_attachments_requests_request_id" FOREIGN KEY (request_id) REFERENCES public.requests(id) ON DELETE RESTRICT;


--
-- Name: request_attachments FK_request_attachments_users_uploaded_by_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_attachments
    ADD CONSTRAINT "FK_request_attachments_users_uploaded_by_user_id" FOREIGN KEY (uploaded_by_user_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: request_messages FK_request_messages_requests_request_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_messages
    ADD CONSTRAINT "FK_request_messages_requests_request_id" FOREIGN KEY (request_id) REFERENCES public.requests(id) ON DELETE RESTRICT;


--
-- Name: request_messages FK_request_messages_users_author_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_messages
    ADD CONSTRAINT "FK_request_messages_users_author_user_id" FOREIGN KEY (author_user_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: request_status_history FK_request_status_history_requests_request_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_status_history
    ADD CONSTRAINT "FK_request_status_history_requests_request_id" FOREIGN KEY (request_id) REFERENCES public.requests(id) ON DELETE RESTRICT;


--
-- Name: request_status_history FK_request_status_history_users_changed_by_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.request_status_history
    ADD CONSTRAINT "FK_request_status_history_users_changed_by_user_id" FOREIGN KEY (changed_by_user_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: requests FK_requests_categories_category_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.requests
    ADD CONSTRAINT "FK_requests_categories_category_id" FOREIGN KEY (category_id) REFERENCES public.categories(id) ON DELETE RESTRICT;


--
-- Name: requests FK_requests_condominiums_condominium_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.requests
    ADD CONSTRAINT "FK_requests_condominiums_condominium_id" FOREIGN KEY (condominium_id) REFERENCES public.condominiums(id) ON DELETE RESTRICT;


--
-- Name: requests FK_requests_units_target_unit_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.requests
    ADD CONSTRAINT "FK_requests_units_target_unit_id" FOREIGN KEY (target_unit_id) REFERENCES public.units(id) ON DELETE RESTRICT;


--
-- Name: requests FK_requests_users_author_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.requests
    ADD CONSTRAINT "FK_requests_users_author_user_id" FOREIGN KEY (author_user_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: unit_memberships FK_unit_memberships_units_unit_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.unit_memberships
    ADD CONSTRAINT "FK_unit_memberships_units_unit_id" FOREIGN KEY (unit_id) REFERENCES public.units(id) ON DELETE RESTRICT;


--
-- Name: unit_memberships FK_unit_memberships_users_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.unit_memberships
    ADD CONSTRAINT "FK_unit_memberships_users_user_id" FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: units FK_units_condominium_blocks_block_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.units
    ADD CONSTRAINT "FK_units_condominium_blocks_block_id" FOREIGN KEY (block_id) REFERENCES public.condominium_blocks(id) ON DELETE RESTRICT;


--
-- Name: units FK_units_condominiums_condominium_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.units
    ADD CONSTRAINT "FK_units_condominiums_condominium_id" FOREIGN KEY (condominium_id) REFERENCES public.condominiums(id) ON DELETE RESTRICT;


--
-- Name: whatsapp_draft_attachments FK_whatsapp_draft_attachments_whatsapp_sessions_session_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_draft_attachments
    ADD CONSTRAINT "FK_whatsapp_draft_attachments_whatsapp_sessions_session_id" FOREIGN KEY (session_id) REFERENCES public.whatsapp_sessions(id) ON DELETE CASCADE;


--
-- Name: whatsapp_inbound_messages FK_whatsapp_inbound_messages_users_identified_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_inbound_messages
    ADD CONSTRAINT "FK_whatsapp_inbound_messages_users_identified_user_id" FOREIGN KEY (identified_user_id) REFERENCES public.users(id) ON DELETE SET NULL;


--
-- Name: whatsapp_outbound_messages FK_whatsapp_outbound_messages_condominiums_condominium_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_outbound_messages
    ADD CONSTRAINT "FK_whatsapp_outbound_messages_condominiums_condominium_id" FOREIGN KEY (condominium_id) REFERENCES public.condominiums(id) ON DELETE RESTRICT;


--
-- Name: whatsapp_outbound_messages FK_whatsapp_outbound_messages_request_messages_request_message~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_outbound_messages
    ADD CONSTRAINT "FK_whatsapp_outbound_messages_request_messages_request_message~" FOREIGN KEY (request_message_id) REFERENCES public.request_messages(id) ON DELETE RESTRICT;


--
-- Name: whatsapp_outbound_messages FK_whatsapp_outbound_messages_requests_request_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_outbound_messages
    ADD CONSTRAINT "FK_whatsapp_outbound_messages_requests_request_id" FOREIGN KEY (request_id) REFERENCES public.requests(id) ON DELETE RESTRICT;


--
-- Name: whatsapp_outbound_messages FK_whatsapp_outbound_messages_users_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_outbound_messages
    ADD CONSTRAINT "FK_whatsapp_outbound_messages_users_user_id" FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: whatsapp_sessions FK_whatsapp_sessions_categories_category_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_sessions
    ADD CONSTRAINT "FK_whatsapp_sessions_categories_category_id" FOREIGN KEY (category_id) REFERENCES public.categories(id) ON DELETE SET NULL;


--
-- Name: whatsapp_sessions FK_whatsapp_sessions_condominiums_condominium_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_sessions
    ADD CONSTRAINT "FK_whatsapp_sessions_condominiums_condominium_id" FOREIGN KEY (condominium_id) REFERENCES public.condominiums(id) ON DELETE SET NULL;


--
-- Name: whatsapp_sessions FK_whatsapp_sessions_requests_request_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_sessions
    ADD CONSTRAINT "FK_whatsapp_sessions_requests_request_id" FOREIGN KEY (request_id) REFERENCES public.requests(id) ON DELETE SET NULL;


--
-- Name: whatsapp_sessions FK_whatsapp_sessions_units_unit_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_sessions
    ADD CONSTRAINT "FK_whatsapp_sessions_units_unit_id" FOREIGN KEY (unit_id) REFERENCES public.units(id) ON DELETE SET NULL;


--
-- Name: whatsapp_sessions FK_whatsapp_sessions_users_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.whatsapp_sessions
    ADD CONSTRAINT "FK_whatsapp_sessions_users_user_id" FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE SET NULL;


--
-- PostgreSQL database dump complete
--

\unrestrict Z7KRZSZ0vfRlCIuiU2oxbyUkzAcQ9bbT9ccCq5N6eJ5m2EUps6w2Ufzcb8y3oyB

