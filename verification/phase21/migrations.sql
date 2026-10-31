START TRANSACTION;
CREATE TABLE finance_accounts (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_finance_accounts" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_accounts_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "AK_finance_accounts_Id_InstitutionId_Currency" UNIQUE ("Id", "InstitutionId", "Currency"),
    CONSTRAINT "CK_finance_accounts_currency" CHECK ("Currency" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')),
    CONSTRAINT "CK_p21_account" CHECK ("Status" IN ('Active','Frozen')),
    CONSTRAINT "FK_finance_accounts_identity_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_accounts_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_authorities (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Permissions" integer NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_finance_authorities" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_authorities_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p21_authority" CHECK ("Permissions" BETWEEN 0 AND 255),
    CONSTRAINT "FK_finance_authorities_identity_users_UserId" FOREIGN KEY ("UserId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_authorities_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_commands (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "RequestId" uuid NOT NULL,
    "Fingerprint" character varying(64) NOT NULL,
    "ResultJson" jsonb NOT NULL,
    CONSTRAINT "PK_finance_commands" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_commands_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "FK_finance_commands_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_fee_schedules (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "ProgramId" uuid NOT NULL,
    "TermId" uuid,
    "AcademicYear" integer,
    "Currency" character varying(3) NOT NULL,
    "Code" character varying(60) NOT NULL,
    "Revision" integer NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "EffectiveUntil" date NOT NULL,
    "Status" character varying(20) NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_finance_fee_schedules" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_fee_schedules_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "AK_finance_fee_schedules_Id_InstitutionId_Currency" UNIQUE ("Id", "InstitutionId", "Currency"),
    CONSTRAINT "CK_finance_fee_schedules_currency" CHECK ("Currency" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')),
    CONSTRAINT "CK_p21_schedule" CHECK ("Status" IN ('Draft','Published','Retired') AND "Revision" > 0 AND "EffectiveFrom" < "EffectiveUntil" AND (("TermId" IS NOT NULL AND "AcademicYear" IS NULL) OR ("TermId" IS NULL AND "AcademicYear" BETWEEN 2000 AND 2200))),
    CONSTRAINT "FK_finance_fee_schedules_academic_programs_ProgramId_Instituti~" FOREIGN KEY ("ProgramId", "InstitutionId") REFERENCES academic_programs ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_fee_schedules_academic_terms_TermId_InstitutionId" FOREIGN KEY ("TermId", "InstitutionId") REFERENCES academic_terms ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_fee_schedules_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_policies (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Revision" integer NOT NULL,
    "TimeZoneId" character varying(100) NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "EffectiveUntil" date NOT NULL,
    "Status" character varying(20) NOT NULL,
    "RequirePaymentVerification" boolean NOT NULL,
    "AllowRefunds" boolean NOT NULL,
    "BlockRefundWithDebt" boolean NOT NULL,
    "MaximumInstallments" integer NOT NULL,
    "MaximumExtensionDays" integer NOT NULL,
    "RequirePlanApproval" boolean NOT NULL,
    "GraceDays" integer NOT NULL,
    "OverdueThresholdMinor" bigint NOT NULL,
    "BlockRegistration" boolean NOT NULL,
    "BlockGraduation" boolean NOT NULL,
    "BlockDocuments" boolean NOT NULL,
    "PublishedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_finance_policies" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_policies_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "AK_finance_policies_Id_InstitutionId_Currency" UNIQUE ("Id", "InstitutionId", "Currency"),
    CONSTRAINT "CK_finance_policies_currency" CHECK ("Currency" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')),
    CONSTRAINT "CK_p21_policy" CHECK ("Status" IN ('Published','Retired') AND "EffectiveFrom" < "EffectiveUntil" AND "Revision" > 0 AND "MaximumInstallments" BETWEEN 1 AND 60 AND "MaximumExtensionDays" BETWEEN 1 AND 730 AND "GraceDays" BETWEEN 0 AND 365 AND "OverdueThresholdMinor" BETWEEN 0 AND 9000000000000000),
    CONSTRAINT "FK_finance_policies_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_reconciliations (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "SourceName" character varying(160) NOT NULL,
    "ProvidedRowsJson" jsonb NOT NULL,
    "ResultsJson" jsonb NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_finance_reconciliations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_reconciliations_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "FK_finance_reconciliations_identity_users_ActorId" FOREIGN KEY ("ActorId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_reconciliations_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_scholarships (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "ProgramId" uuid,
    "TermId" uuid,
    "Currency" character varying(3) NOT NULL,
    "Code" character varying(60) NOT NULL,
    "Revision" integer NOT NULL,
    "Priority" integer NOT NULL,
    "Category" character varying(20) NOT NULL,
    "Mode" character varying(15) NOT NULL,
    "BasisPoints" integer NOT NULL,
    "FixedMinor" bigint NOT NULL,
    "FundingLimitMinor" bigint,
    "MinimumGpa" numeric(4,2),
    "RequireGoodStanding" boolean NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "EffectiveUntil" date NOT NULL,
    CONSTRAINT "PK_finance_scholarships" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_scholarships_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_finance_scholarships_currency" CHECK ("Currency" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')),
    CONSTRAINT "CK_p21_scholarship" CHECK ("Revision" > 0 AND "EffectiveFrom" < "EffectiveUntil" AND "Mode" IN ('Percent','Fixed','Full') AND "BasisPoints" BETWEEN 0 AND 10000 AND "FixedMinor" BETWEEN 0 AND 9000000000000000 AND ("Mode" <> 'Percent' OR "BasisPoints" > 0) AND ("Mode" <> 'Fixed' OR "FixedMinor" > 0) AND ("FundingLimitMinor" IS NULL OR "FundingLimitMinor" BETWEEN 0 AND 9000000000000000) AND ("MinimumGpa" IS NULL OR "MinimumGpa" BETWEEN 0 AND 4)),
    CONSTRAINT "FK_finance_scholarships_academic_programs_ProgramId_Institutio~" FOREIGN KEY ("ProgramId", "InstitutionId") REFERENCES academic_programs ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_scholarships_academic_terms_TermId_InstitutionId" FOREIGN KEY ("TermId", "InstitutionId") REFERENCES academic_terms ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_scholarships_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_audit_events (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "AccountId" uuid,
    "EntityId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "Action" character varying(80) NOT NULL,
    "Detail" character varying(4000) NOT NULL,
    "AtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_finance_audit_events" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_audit_events_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "FK_finance_audit_events_finance_accounts_AccountId_Institution~" FOREIGN KEY ("AccountId", "InstitutionId") REFERENCES finance_accounts ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_audit_events_identity_users_ActorId" FOREIGN KEY ("ActorId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_audit_events_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_payments (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Reference" character varying(120) NOT NULL,
    "Method" character varying(25) NOT NULL,
    "AmountMinor" bigint NOT NULL,
    "RequiresVerification" boolean NOT NULL,
    "Status" character varying(20) NOT NULL,
    "RegisteredBy" uuid NOT NULL,
    "VerifiedBy" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "PostedAtUtc" timestamp with time zone,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_finance_payments" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_payments_Id_AccountId_InstitutionId_Currency" UNIQUE ("Id", "AccountId", "InstitutionId", "Currency"),
    CONSTRAINT "AK_finance_payments_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_finance_payments_currency" CHECK ("Currency" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')),
    CONSTRAINT "CK_p21_payment" CHECK ("AmountMinor" BETWEEN 1 AND 9000000000000000 AND "Status" IN ('Pending','Verified','Posted','Rejected','Reversed') AND "Method" IN ('Cash','BankTransfer','ManualTransfer','ExternalReference') AND ("Status" NOT IN ('Verified','Posted','Reversed') OR NOT "RequiresVerification" OR ("VerifiedBy" IS NOT NULL AND "VerifiedBy" <> "RegisteredBy"))),
    CONSTRAINT "FK_finance_payments_finance_accounts_AccountId_InstitutionId_C~" FOREIGN KEY ("AccountId", "InstitutionId", "Currency") REFERENCES finance_accounts ("Id", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_payments_identity_users_RegisteredBy" FOREIGN KEY ("RegisteredBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_payments_identity_users_VerifiedBy" FOREIGN KEY ("VerifiedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_payments_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_fee_items (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "ScheduleId" uuid NOT NULL,
    "CourseId" uuid,
    "Code" character varying(60) NOT NULL,
    "Category" character varying(20) NOT NULL,
    "Mode" character varying(15) NOT NULL,
    "UnitMinor" bigint NOT NULL,
    "Optional" boolean NOT NULL,
    CONSTRAINT "PK_finance_fee_items" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_fee_items_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p21_fee" CHECK ("UnitMinor" BETWEEN 1 AND 9000000000000000 AND "Mode" IN ('Fixed','PerCredit') AND "Category" IN ('Tuition','Registration','Laboratory','Service','Optional')),
    CONSTRAINT "FK_finance_fee_items_courses_CourseId_InstitutionId" FOREIGN KEY ("CourseId", "InstitutionId") REFERENCES courses (id, institution_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_fee_items_finance_fee_schedules_ScheduleId_Institut~" FOREIGN KEY ("ScheduleId", "InstitutionId") REFERENCES finance_fee_schedules ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_fee_items_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_invoices (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "ProgramId" uuid NOT NULL,
    "TermId" uuid NOT NULL,
    "ScheduleId" uuid NOT NULL,
    "PolicyId" uuid NOT NULL,
    "Scope" character varying(20) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "DueDate" date NOT NULL,
    "PricingEvidenceJson" jsonb NOT NULL,
    "OptionalCodesJson" jsonb NOT NULL,
    "TotalMinor" bigint NOT NULL,
    "Version" bigint NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "PostedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_finance_invoices" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_invoices_Id_AccountId_InstitutionId_Currency" UNIQUE ("Id", "AccountId", "InstitutionId", "Currency"),
    CONSTRAINT "AK_finance_invoices_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_finance_invoices_currency" CHECK ("Currency" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')),
    CONSTRAINT "CK_p21_invoice" CHECK ("Status" IN ('Draft','Posted','Reversed') AND "Scope"='Tuition' AND "TotalMinor" BETWEEN 1 AND 9000000000000000 AND ("Status"='Draft' OR "PostedAtUtc" IS NOT NULL)),
    CONSTRAINT "FK_finance_invoices_academic_programs_ProgramId_InstitutionId" FOREIGN KEY ("ProgramId", "InstitutionId") REFERENCES academic_programs ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_invoices_academic_terms_TermId_InstitutionId" FOREIGN KEY ("TermId", "InstitutionId") REFERENCES academic_terms ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_invoices_finance_accounts_AccountId_InstitutionId_C~" FOREIGN KEY ("AccountId", "InstitutionId", "Currency") REFERENCES finance_accounts ("Id", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_invoices_finance_fee_schedules_ScheduleId_Instituti~" FOREIGN KEY ("ScheduleId", "InstitutionId", "Currency") REFERENCES finance_fee_schedules ("Id", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_invoices_finance_policies_PolicyId_InstitutionId_Cu~" FOREIGN KEY ("PolicyId", "InstitutionId", "Currency") REFERENCES finance_policies ("Id", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_invoices_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_receipts (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "PaymentId" uuid NOT NULL,
    "VerificationKeyId" uuid NOT NULL,
    "Number" character varying(80) NOT NULL,
    "SnapshotJson" jsonb NOT NULL,
    "Proof" character varying(64) NOT NULL,
    "IssuedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_finance_receipts" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_receipts_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "FK_finance_receipts_finance_payments_PaymentId_InstitutionId" FOREIGN KEY ("PaymentId", "InstitutionId") REFERENCES finance_payments ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_receipts_institutional_verification_keys_Verificati~" FOREIGN KEY ("VerificationKeyId", "InstitutionId") REFERENCES institutional_verification_keys ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_receipts_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_refunds (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "PaymentId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "AmountMinor" bigint NOT NULL,
    "Status" character varying(20) NOT NULL,
    "RequestedBy" uuid NOT NULL,
    "ReviewedBy" uuid,
    "PolicyId" uuid,
    "Reason" character varying(1000) NOT NULL,
    "ExecutionReference" character varying(120),
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_finance_refunds" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_refunds_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_finance_refunds_currency" CHECK ("Currency" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')),
    CONSTRAINT "CK_p21_refund" CHECK ("AmountMinor" BETWEEN 1 AND 9000000000000000 AND "Status" IN ('Requested','Approved','Rejected','Executed','Reversed') AND ("Status" NOT IN ('Approved','Executed','Reversed') OR ("ReviewedBy" IS NOT NULL AND "ReviewedBy" <> "RequestedBy")) AND ("Status" NOT IN ('Executed','Reversed') OR "ExecutionReference" IS NOT NULL)),
    CONSTRAINT "FK_finance_refunds_finance_accounts_AccountId_InstitutionId_Cu~" FOREIGN KEY ("AccountId", "InstitutionId", "Currency") REFERENCES finance_accounts ("Id", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_refunds_finance_payments_PaymentId_AccountId_Instit~" FOREIGN KEY ("PaymentId", "AccountId", "InstitutionId", "Currency") REFERENCES finance_payments ("Id", "AccountId", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_refunds_finance_policies_PolicyId_InstitutionId_Cur~" FOREIGN KEY ("PolicyId", "InstitutionId", "Currency") REFERENCES finance_policies ("Id", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_refunds_identity_users_RequestedBy" FOREIGN KEY ("RequestedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_refunds_identity_users_ReviewedBy" FOREIGN KEY ("ReviewedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_refunds_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_awards (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "ScholarshipId" uuid NOT NULL,
    "Status" character varying(20) NOT NULL,
    "RequestedBy" uuid NOT NULL,
    "ReviewedBy" uuid,
    "Reason" character varying(1000) NOT NULL,
    "ApprovalEvidenceJson" jsonb,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_finance_awards" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_awards_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p21_award" CHECK ("Status" IN ('Requested','Approved','Rejected','Revoked') AND ("Status" NOT IN ('Approved','Revoked') OR ("ReviewedBy" IS NOT NULL AND "ReviewedBy" <> "RequestedBy" AND "ApprovalEvidenceJson" IS NOT NULL))),
    CONSTRAINT "FK_finance_awards_finance_invoices_InvoiceId_InstitutionId" FOREIGN KEY ("InvoiceId", "InstitutionId") REFERENCES finance_invoices ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_awards_finance_scholarships_ScholarshipId_Instituti~" FOREIGN KEY ("ScholarshipId", "InstitutionId") REFERENCES finance_scholarships ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_awards_identity_users_RequestedBy" FOREIGN KEY ("RequestedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_awards_identity_users_ReviewedBy" FOREIGN KEY ("ReviewedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_awards_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_invoice_lines (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "FeeItemId" uuid NOT NULL,
    "Code" character varying(60) NOT NULL,
    "Category" character varying(20) NOT NULL,
    "Quantity" numeric(12,2) NOT NULL,
    "UnitMinor" bigint NOT NULL,
    "AmountMinor" bigint NOT NULL,
    CONSTRAINT "PK_finance_invoice_lines" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_invoice_lines_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "AK_finance_invoice_lines_Id_InvoiceId_InstitutionId" UNIQUE ("Id", "InvoiceId", "InstitutionId"),
    CONSTRAINT "CK_p21_invoice_line" CHECK ("AmountMinor" BETWEEN 1 AND 9000000000000000 AND "UnitMinor" BETWEEN 1 AND 9000000000000000 AND "Quantity" > 0),
    CONSTRAINT "FK_finance_invoice_lines_finance_fee_items_FeeItemId_Instituti~" FOREIGN KEY ("FeeItemId", "InstitutionId") REFERENCES finance_fee_items ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_invoice_lines_finance_invoices_InvoiceId_Institutio~" FOREIGN KEY ("InvoiceId", "InstitutionId") REFERENCES finance_invoices ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_invoice_lines_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_journals (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Kind" character varying(35) NOT NULL,
    "SourceId" uuid NOT NULL,
    "InvoiceId" uuid,
    "PaymentId" uuid,
    "ReversesJournalId" uuid,
    "PostingKey" character varying(160) NOT NULL,
    "AmountMinor" bigint NOT NULL,
    "ActorId" uuid NOT NULL,
    "Reason" character varying(1000) NOT NULL,
    "PostedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_finance_journals" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_journals_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_finance_journals_currency" CHECK ("Currency" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')),
    CONSTRAINT "CK_p21_journal_amount" CHECK ("AmountMinor" BETWEEN 1 AND 9000000000000000 AND "Kind" IN ('InvoicePosted','DebitAdjustment','CreditNote','PaymentPosted','PaymentReversed','Allocation','AllocationReversal','ScholarshipGranted','ScholarshipReclaimed','RefundExecuted','RefundReversed')),
    CONSTRAINT "FK_finance_journals_finance_accounts_AccountId_InstitutionId_C~" FOREIGN KEY ("AccountId", "InstitutionId", "Currency") REFERENCES finance_accounts ("Id", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_journals_finance_invoices_InvoiceId_AccountId_Insti~" FOREIGN KEY ("InvoiceId", "AccountId", "InstitutionId", "Currency") REFERENCES finance_invoices ("Id", "AccountId", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_journals_finance_journals_ReversesJournalId_Institu~" FOREIGN KEY ("ReversesJournalId", "InstitutionId") REFERENCES finance_journals ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_journals_finance_payments_PaymentId_AccountId_Insti~" FOREIGN KEY ("PaymentId", "AccountId", "InstitutionId", "Currency") REFERENCES finance_payments ("Id", "AccountId", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_journals_identity_users_ActorId" FOREIGN KEY ("ActorId") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_journals_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_payment_plans (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "Revision" integer NOT NULL,
    "Status" character varying(20) NOT NULL,
    "BaseDebtMinor" bigint NOT NULL,
    "RequestedBy" uuid NOT NULL,
    "ReviewedBy" uuid,
    "Reason" character varying(1000) NOT NULL,
    "Version" bigint NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ApprovedAtUtc" timestamp with time zone,
    "SupersededAtUtc" timestamp with time zone,
    CONSTRAINT "PK_finance_payment_plans" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_payment_plans_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p21_plan" CHECK ("Status" IN ('Proposed','Approved','Rejected','Superseded') AND "Revision" > 0 AND "BaseDebtMinor" BETWEEN 1 AND 9000000000000000 AND ("Status" NOT IN ('Approved','Superseded') OR "ApprovedAtUtc" IS NOT NULL) AND ("Status" <> 'Superseded' OR "SupersededAtUtc" IS NOT NULL)),
    CONSTRAINT "FK_finance_payment_plans_finance_invoices_InvoiceId_Institutio~" FOREIGN KEY ("InvoiceId", "InstitutionId") REFERENCES finance_invoices ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_payment_plans_identity_users_RequestedBy" FOREIGN KEY ("RequestedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_payment_plans_identity_users_ReviewedBy" FOREIGN KEY ("ReviewedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_payment_plans_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_adjustments (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "InvoiceLineId" uuid NOT NULL,
    "JournalId" uuid NOT NULL,
    "SignedMinor" bigint NOT NULL,
    CONSTRAINT "PK_finance_adjustments" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_adjustments_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p21_adjustment" CHECK ("SignedMinor" <> 0 AND "SignedMinor" BETWEEN -9000000000000000 AND 9000000000000000),
    CONSTRAINT "FK_finance_adjustments_finance_invoice_lines_InvoiceLineId_Inv~" FOREIGN KEY ("InvoiceLineId", "InvoiceId", "InstitutionId") REFERENCES finance_invoice_lines ("Id", "InvoiceId", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_adjustments_finance_invoices_InvoiceId_InstitutionId" FOREIGN KEY ("InvoiceId", "InstitutionId") REFERENCES finance_invoices ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_adjustments_finance_journals_JournalId_InstitutionId" FOREIGN KEY ("JournalId", "InstitutionId") REFERENCES finance_journals ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_adjustments_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_allocations (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "PaymentId" uuid NOT NULL,
    "JournalId" uuid NOT NULL,
    "OriginalAllocationId" uuid,
    "SignedMinor" bigint NOT NULL,
    CONSTRAINT "PK_finance_allocations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_allocations_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_finance_allocations_currency" CHECK ("Currency" IN ('AED','AFN','ALL','AMD','AOA','ARS','AUD','AWG','AZN','BAM','BBD','BDT','BHD','BIF','BMD','BND','BOB','BOV','BRL','BSD','BTN','BWP','BYN','BZD','CAD','CDF','CHE','CHF','CHW','CLF','CLP','CNY','COP','COU','CRC','CUP','CVE','CZK','DJF','DKK','DOP','DZD','EGP','ERN','ETB','EUR','FJD','FKP','GBP','GEL','GHS','GIP','GMD','GNF','GTQ','GYD','HKD','HNL','HTG','HUF','IDR','ILS','INR','IQD','IRR','ISK','JMD','JOD','JPY','KES','KGS','KHR','KMF','KPW','KRW','KWD','KYD','KZT','LAK','LBP','LKR','LRD','LSL','LYD','MAD','MDL','MGA','MKD','MMK','MNT','MOP','MRU','MUR','MVR','MWK','MXN','MXV','MYR','MZN','NAD','NGN','NIO','NOK','NPR','NZD','OMR','PAB','PEN','PGK','PHP','PKR','PLN','PYG','QAR','RON','RSD','RUB','RWF','SAR','SBD','SCR','SDG','SEK','SGD','SHP','SLE','SOS','SRD','SSP','STN','SVC','SYP','SZL','THB','TJS','TMT','TND','TOP','TRY','TTD','TWD','TZS','UAH','UGX','USD','USN','UYI','UYU','UYW','UZS','VED','VES','VND','VUV','WST','XAD','XAF','XCD','XCG','XOF','XPF','YER','ZAR','ZMW','ZWG')),
    CONSTRAINT "CK_p21_allocation" CHECK (("SignedMinor" > 0 AND "OriginalAllocationId" IS NULL) OR ("SignedMinor" < 0 AND "OriginalAllocationId" IS NOT NULL)),
    CONSTRAINT "FK_finance_allocations_finance_accounts_AccountId_InstitutionI~" FOREIGN KEY ("AccountId", "InstitutionId", "Currency") REFERENCES finance_accounts ("Id", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_allocations_finance_allocations_OriginalAllocationI~" FOREIGN KEY ("OriginalAllocationId", "InstitutionId") REFERENCES finance_allocations ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_allocations_finance_invoices_InvoiceId_AccountId_In~" FOREIGN KEY ("InvoiceId", "AccountId", "InstitutionId", "Currency") REFERENCES finance_invoices ("Id", "AccountId", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_allocations_finance_journals_JournalId_InstitutionId" FOREIGN KEY ("JournalId", "InstitutionId") REFERENCES finance_journals ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_allocations_finance_payments_PaymentId_AccountId_In~" FOREIGN KEY ("PaymentId", "AccountId", "InstitutionId", "Currency") REFERENCES finance_payments ("Id", "AccountId", "InstitutionId", "Currency") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_allocations_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_journal_lines (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "JournalId" uuid NOT NULL,
    "Bucket" character varying(20) NOT NULL,
    "SignedMinor" bigint NOT NULL,
    CONSTRAINT "PK_finance_journal_lines" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_journal_lines_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p21_leg" CHECK ("SignedMinor" <> 0 AND "SignedMinor" BETWEEN -9000000000000000 AND 9000000000000000 AND "Bucket" IN ('Receivable','StudentCredit','Funds','Revenue','Aid')),
    CONSTRAINT "FK_finance_journal_lines_finance_journals_JournalId_Institutio~" FOREIGN KEY ("JournalId", "InstitutionId") REFERENCES finance_journals ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_journal_lines_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_holds (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "PolicyId" uuid NOT NULL,
    "PlanId" uuid,
    "Scope" character varying(20) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "Reason" character varying(1000) NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ReleasedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_finance_holds" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_holds_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p21_hold" CHECK ("Status" IN ('Open','Released') AND "Scope" IN ('Registration','Graduation','Documents') AND ("Status" <> 'Released' OR "ReleasedAtUtc" IS NOT NULL)),
    CONSTRAINT "FK_finance_holds_finance_accounts_AccountId_InstitutionId" FOREIGN KEY ("AccountId", "InstitutionId") REFERENCES finance_accounts ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_holds_finance_invoices_InvoiceId_InstitutionId" FOREIGN KEY ("InvoiceId", "InstitutionId") REFERENCES finance_invoices ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_holds_finance_payment_plans_PlanId_InstitutionId" FOREIGN KEY ("PlanId", "InstitutionId") REFERENCES finance_payment_plans ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_holds_finance_policies_PolicyId_InstitutionId" FOREIGN KEY ("PolicyId", "InstitutionId") REFERENCES finance_policies ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_holds_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE TABLE finance_installments (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "PlanId" uuid NOT NULL,
    "Position" integer NOT NULL,
    "DueDate" date NOT NULL,
    "AmountMinor" bigint NOT NULL,
    CONSTRAINT "PK_finance_installments" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_finance_installments_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p21_installment" CHECK ("Position" > 0 AND "AmountMinor" BETWEEN 1 AND 9000000000000000),
    CONSTRAINT "FK_finance_installments_finance_payment_plans_PlanId_Instituti~" FOREIGN KEY ("PlanId", "InstitutionId") REFERENCES finance_payment_plans ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_finance_installments_institutions_InstitutionId" FOREIGN KEY ("InstitutionId") REFERENCES institutions ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_finance_accounts_InstitutionId_StudentId_Currency" ON finance_accounts ("InstitutionId", "StudentId", "Currency");

CREATE INDEX "IX_finance_accounts_StudentId" ON finance_accounts ("StudentId");

CREATE INDEX "IX_finance_adjustments_InstitutionId" ON finance_adjustments ("InstitutionId");

CREATE INDEX "IX_finance_adjustments_InvoiceId_InstitutionId" ON finance_adjustments ("InvoiceId", "InstitutionId");

CREATE INDEX "IX_finance_adjustments_InvoiceLineId_InvoiceId_InstitutionId" ON finance_adjustments ("InvoiceLineId", "InvoiceId", "InstitutionId");

CREATE UNIQUE INDEX "IX_finance_adjustments_JournalId" ON finance_adjustments ("JournalId");

CREATE INDEX "IX_finance_adjustments_JournalId_InstitutionId" ON finance_adjustments ("JournalId", "InstitutionId");

CREATE INDEX "IX_finance_allocations_AccountId_InstitutionId_Currency" ON finance_allocations ("AccountId", "InstitutionId", "Currency");

CREATE INDEX "IX_finance_allocations_InstitutionId" ON finance_allocations ("InstitutionId");

CREATE INDEX "IX_finance_allocations_InvoiceId_AccountId_InstitutionId_Curre~" ON finance_allocations ("InvoiceId", "AccountId", "InstitutionId", "Currency");

CREATE UNIQUE INDEX "IX_finance_allocations_JournalId" ON finance_allocations ("JournalId");

CREATE INDEX "IX_finance_allocations_JournalId_InstitutionId" ON finance_allocations ("JournalId", "InstitutionId");

CREATE INDEX "IX_finance_allocations_OriginalAllocationId_InstitutionId" ON finance_allocations ("OriginalAllocationId", "InstitutionId");

CREATE INDEX "IX_finance_allocations_PaymentId_AccountId_InstitutionId_Curre~" ON finance_allocations ("PaymentId", "AccountId", "InstitutionId", "Currency");

CREATE INDEX "IX_finance_audit_events_AccountId_InstitutionId" ON finance_audit_events ("AccountId", "InstitutionId");

CREATE INDEX "IX_finance_audit_events_ActorId" ON finance_audit_events ("ActorId");

CREATE INDEX "IX_finance_audit_events_InstitutionId_AccountId_AtUtc" ON finance_audit_events ("InstitutionId", "AccountId", "AtUtc");

CREATE UNIQUE INDEX "IX_finance_authorities_InstitutionId_UserId" ON finance_authorities ("InstitutionId", "UserId");

CREATE INDEX "IX_finance_authorities_UserId" ON finance_authorities ("UserId");

CREATE INDEX "IX_finance_awards_InstitutionId" ON finance_awards ("InstitutionId");

CREATE INDEX "IX_finance_awards_InvoiceId_InstitutionId" ON finance_awards ("InvoiceId", "InstitutionId");

CREATE UNIQUE INDEX "IX_finance_awards_InvoiceId_ScholarshipId" ON finance_awards ("InvoiceId", "ScholarshipId") WHERE "Status" IN ('Requested','Approved');

CREATE INDEX "IX_finance_awards_RequestedBy" ON finance_awards ("RequestedBy");

CREATE INDEX "IX_finance_awards_ReviewedBy" ON finance_awards ("ReviewedBy");

CREATE INDEX "IX_finance_awards_ScholarshipId_InstitutionId" ON finance_awards ("ScholarshipId", "InstitutionId");

CREATE UNIQUE INDEX "IX_finance_commands_InstitutionId_RequestId" ON finance_commands ("InstitutionId", "RequestId");

CREATE INDEX "IX_finance_fee_items_CourseId_InstitutionId" ON finance_fee_items ("CourseId", "InstitutionId");

CREATE INDEX "IX_finance_fee_items_InstitutionId" ON finance_fee_items ("InstitutionId");

CREATE UNIQUE INDEX "IX_finance_fee_items_ScheduleId_Code" ON finance_fee_items ("ScheduleId", "Code");

CREATE INDEX "IX_finance_fee_items_ScheduleId_InstitutionId" ON finance_fee_items ("ScheduleId", "InstitutionId");

CREATE UNIQUE INDEX "IX_finance_fee_schedules_InstitutionId_Code_Currency_Revision" ON finance_fee_schedules ("InstitutionId", "Code", "Currency", "Revision");

CREATE INDEX "IX_finance_fee_schedules_ProgramId_InstitutionId" ON finance_fee_schedules ("ProgramId", "InstitutionId");

CREATE INDEX "IX_finance_fee_schedules_TermId_InstitutionId" ON finance_fee_schedules ("TermId", "InstitutionId");

CREATE INDEX "IX_finance_holds_AccountId_InstitutionId" ON finance_holds ("AccountId", "InstitutionId");

CREATE INDEX "IX_finance_holds_AccountId_Status_EffectiveFrom" ON finance_holds ("AccountId", "Status", "EffectiveFrom");

CREATE INDEX "IX_finance_holds_InstitutionId" ON finance_holds ("InstitutionId");

CREATE INDEX "IX_finance_holds_InvoiceId_InstitutionId" ON finance_holds ("InvoiceId", "InstitutionId");

CREATE UNIQUE INDEX "IX_finance_holds_InvoiceId_Scope" ON finance_holds ("InvoiceId", "Scope") WHERE "Status"='Open';

CREATE INDEX "IX_finance_holds_PlanId_InstitutionId" ON finance_holds ("PlanId", "InstitutionId");

CREATE INDEX "IX_finance_holds_PolicyId_InstitutionId" ON finance_holds ("PolicyId", "InstitutionId");

CREATE INDEX "IX_finance_installments_InstitutionId" ON finance_installments ("InstitutionId");

CREATE INDEX "IX_finance_installments_PlanId_InstitutionId" ON finance_installments ("PlanId", "InstitutionId");

CREATE UNIQUE INDEX "IX_finance_installments_PlanId_Position" ON finance_installments ("PlanId", "Position");

CREATE INDEX "IX_finance_invoice_lines_FeeItemId_InstitutionId" ON finance_invoice_lines ("FeeItemId", "InstitutionId");

CREATE INDEX "IX_finance_invoice_lines_InstitutionId" ON finance_invoice_lines ("InstitutionId");

CREATE INDEX "IX_finance_invoice_lines_InvoiceId_InstitutionId" ON finance_invoice_lines ("InvoiceId", "InstitutionId");

CREATE INDEX "IX_finance_invoices_AccountId_InstitutionId_Currency" ON finance_invoices ("AccountId", "InstitutionId", "Currency");

CREATE UNIQUE INDEX "IX_finance_invoices_AccountId_TermId_Scope" ON finance_invoices ("AccountId", "TermId", "Scope");

CREATE INDEX "IX_finance_invoices_InstitutionId_Currency_DueDate" ON finance_invoices ("InstitutionId", "Currency", "DueDate");

CREATE INDEX "IX_finance_invoices_PolicyId_InstitutionId_Currency" ON finance_invoices ("PolicyId", "InstitutionId", "Currency");

CREATE INDEX "IX_finance_invoices_ProgramId_InstitutionId" ON finance_invoices ("ProgramId", "InstitutionId");

CREATE INDEX "IX_finance_invoices_ScheduleId_InstitutionId_Currency" ON finance_invoices ("ScheduleId", "InstitutionId", "Currency");

CREATE INDEX "IX_finance_invoices_TermId_InstitutionId" ON finance_invoices ("TermId", "InstitutionId");

CREATE INDEX "IX_finance_journal_lines_InstitutionId" ON finance_journal_lines ("InstitutionId");

CREATE UNIQUE INDEX "IX_finance_journal_lines_JournalId_Bucket" ON finance_journal_lines ("JournalId", "Bucket");

CREATE INDEX "IX_finance_journal_lines_JournalId_InstitutionId" ON finance_journal_lines ("JournalId", "InstitutionId");

CREATE INDEX "IX_finance_journals_AccountId_InstitutionId_Currency" ON finance_journals ("AccountId", "InstitutionId", "Currency");

CREATE INDEX "IX_finance_journals_AccountId_PostedAtUtc" ON finance_journals ("AccountId", "PostedAtUtc");

CREATE INDEX "IX_finance_journals_ActorId" ON finance_journals ("ActorId");

CREATE UNIQUE INDEX "IX_finance_journals_InstitutionId_PostingKey" ON finance_journals ("InstitutionId", "PostingKey");

CREATE INDEX "IX_finance_journals_InvoiceId_AccountId_InstitutionId_Currency" ON finance_journals ("InvoiceId", "AccountId", "InstitutionId", "Currency");

CREATE UNIQUE INDEX "IX_finance_journals_Kind_SourceId" ON finance_journals ("Kind", "SourceId") WHERE "Kind" IN ('InvoicePosted','PaymentPosted','PaymentReversed','RefundExecuted','RefundReversed');

CREATE INDEX "IX_finance_journals_PaymentId_AccountId_InstitutionId_Currency" ON finance_journals ("PaymentId", "AccountId", "InstitutionId", "Currency");

CREATE UNIQUE INDEX "IX_finance_journals_ReversesJournalId" ON finance_journals ("ReversesJournalId");

CREATE INDEX "IX_finance_journals_ReversesJournalId_InstitutionId" ON finance_journals ("ReversesJournalId", "InstitutionId");

CREATE INDEX "IX_finance_payment_plans_InstitutionId" ON finance_payment_plans ("InstitutionId");

CREATE INDEX "IX_finance_payment_plans_InvoiceId_InstitutionId" ON finance_payment_plans ("InvoiceId", "InstitutionId");

CREATE UNIQUE INDEX "IX_finance_payment_plans_InvoiceId_Revision" ON finance_payment_plans ("InvoiceId", "Revision");

CREATE INDEX "IX_finance_payment_plans_RequestedBy" ON finance_payment_plans ("RequestedBy");

CREATE INDEX "IX_finance_payment_plans_ReviewedBy" ON finance_payment_plans ("ReviewedBy");

CREATE UNIQUE INDEX "IX_finance_plan_approved" ON finance_payment_plans ("InvoiceId") WHERE "Status"='Approved';

CREATE UNIQUE INDEX "IX_finance_plan_proposed" ON finance_payment_plans ("InvoiceId") WHERE "Status"='Proposed';

CREATE INDEX "IX_finance_payments_AccountId_InstitutionId_Currency" ON finance_payments ("AccountId", "InstitutionId", "Currency");

CREATE UNIQUE INDEX "IX_finance_payments_InstitutionId_Reference" ON finance_payments ("InstitutionId", "Reference") WHERE "Status" <> 'Rejected';

CREATE INDEX "IX_finance_payments_RegisteredBy" ON finance_payments ("RegisteredBy");

CREATE INDEX "IX_finance_payments_VerifiedBy" ON finance_payments ("VerifiedBy");

CREATE UNIQUE INDEX "IX_finance_policies_InstitutionId_Currency_Revision" ON finance_policies ("InstitutionId", "Currency", "Revision");

CREATE INDEX "IX_finance_receipts_InstitutionId" ON finance_receipts ("InstitutionId");

CREATE UNIQUE INDEX "IX_finance_receipts_Number" ON finance_receipts ("Number");

CREATE UNIQUE INDEX "IX_finance_receipts_PaymentId" ON finance_receipts ("PaymentId");

CREATE INDEX "IX_finance_receipts_PaymentId_InstitutionId" ON finance_receipts ("PaymentId", "InstitutionId");

CREATE INDEX "IX_finance_receipts_VerificationKeyId_InstitutionId" ON finance_receipts ("VerificationKeyId", "InstitutionId");

CREATE INDEX "IX_finance_reconciliations_ActorId" ON finance_reconciliations ("ActorId");

CREATE INDEX "IX_finance_reconciliations_InstitutionId" ON finance_reconciliations ("InstitutionId");

CREATE INDEX "IX_finance_refunds_AccountId_InstitutionId_Currency" ON finance_refunds ("AccountId", "InstitutionId", "Currency");

CREATE UNIQUE INDEX "IX_finance_refunds_InstitutionId_ExecutionReference" ON finance_refunds ("InstitutionId", "ExecutionReference") WHERE "ExecutionReference" IS NOT NULL;

CREATE INDEX "IX_finance_refunds_PaymentId_AccountId_InstitutionId_Currency" ON finance_refunds ("PaymentId", "AccountId", "InstitutionId", "Currency");

CREATE INDEX "IX_finance_refunds_PolicyId_InstitutionId_Currency" ON finance_refunds ("PolicyId", "InstitutionId", "Currency");

CREATE INDEX "IX_finance_refunds_RequestedBy" ON finance_refunds ("RequestedBy");

CREATE INDEX "IX_finance_refunds_ReviewedBy" ON finance_refunds ("ReviewedBy");

CREATE UNIQUE INDEX "IX_finance_scholarships_InstitutionId_Code_Currency_Revision" ON finance_scholarships ("InstitutionId", "Code", "Currency", "Revision");

CREATE INDEX "IX_finance_scholarships_ProgramId_InstitutionId" ON finance_scholarships ("ProgramId", "InstitutionId");

CREATE INDEX "IX_finance_scholarships_TermId_InstitutionId" ON finance_scholarships ("TermId", "InstitutionId");

ALTER TABLE finance_payments DROP CONSTRAINT "CK_p21_payment";

ALTER TABLE finance_invoice_lines DROP CONSTRAINT "CK_p21_invoice_line";

ALTER TABLE finance_payments ADD CONSTRAINT "CK_p21_payment" CHECK ("AmountMinor" BETWEEN 1 AND 9000000000000000 AND "Reference"=upper(btrim("Reference")) AND "Status" IN ('Pending','Verified','Posted','Rejected','Reversed') AND "Method" IN ('Cash','BankTransfer','ManualTransfer','ExternalReference') AND ("Status" NOT IN ('Posted','Reversed') OR "PostedAtUtc" IS NOT NULL) AND ("Status" NOT IN ('Verified','Posted','Reversed') OR NOT "RequiresVerification" OR ("VerifiedBy" IS NOT NULL AND "VerifiedBy" <> "RegisteredBy")));

CREATE INDEX "IX_finance_journals_SourceId" ON finance_journals ("SourceId");

ALTER TABLE finance_invoice_lines ADD CONSTRAINT "CK_p21_invoice_line" CHECK ("AmountMinor" BETWEEN 1 AND 9000000000000000 AND "UnitMinor" BETWEEN 1 AND 9000000000000000 AND "Quantity" > 0 AND "Quantity" <= 10000 AND "AmountMinor"=round("UnitMinor"::numeric*"Quantity",0));

DROP INDEX "IX_finance_awards_ScholarshipId_InstitutionId";

ALTER TABLE finance_awards ADD "ProgramCode" character varying(60) NOT NULL DEFAULT '';

ALTER TABLE finance_accounts ADD "Version" bigint NOT NULL DEFAULT 1;

ALTER TABLE finance_scholarships ADD CONSTRAINT "AK_finance_scholarships_Id_InstitutionId_Code" UNIQUE ("Id", "InstitutionId", "Code");

CREATE UNIQUE INDEX "IX_finance_awards_InvoiceId_ProgramCode" ON finance_awards ("InvoiceId", "ProgramCode") WHERE "Status" IN ('Requested','Approved');

CREATE INDEX "IX_finance_awards_ScholarshipId_InstitutionId_ProgramCode" ON finance_awards ("ScholarshipId", "InstitutionId", "ProgramCode");

ALTER TABLE finance_awards ADD CONSTRAINT "FK_finance_awards_finance_scholarships_ScholarshipId_Institut~1" FOREIGN KEY ("ScholarshipId", "InstitutionId", "ProgramCode") REFERENCES finance_scholarships ("Id", "InstitutionId", "Code") ON DELETE RESTRICT;

CREATE FUNCTION p21_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Posted finance evidence/history is append-only; use correcting entries' USING ERRCODE='23514'; END $$;

CREATE FUNCTION p21_user_scope() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE field_name text; user_id uuid; scope_id uuid;
BEGIN
 FOREACH field_name IN ARRAY TG_ARGV LOOP
  user_id:=(to_jsonb(NEW)->>field_name)::uuid;
  IF user_id IS NOT NULL THEN
   SELECT "InstitutionId" INTO scope_id FROM identity_users WHERE "Id"=user_id;
   IF scope_id IS DISTINCT FROM NEW."InstitutionId" AND (field_name='StudentId' OR NOT EXISTS
     (SELECT 1 FROM "AspNetUserRoles" ur JOIN identity_roles r ON r."Id"=ur."RoleId" WHERE ur."UserId"=user_id AND r."Name"='SuperAdministrator'))
   THEN RAISE EXCEPTION 'Finance identity/institution mismatch' USING ERRCODE='23514'; END IF;
  END IF;
 END LOOP; RETURN NEW;
END $$;

CREATE FUNCTION p21_state_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE old_json jsonb; new_json jsonb; permitted text[]; old_status text; new_status text;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Financial records cannot be deleted' USING ERRCODE='23514'; END IF;
 old_json:=to_jsonb(OLD);new_json:=to_jsonb(NEW);old_status:=old_json->>'Status';new_status:=new_json->>'Status';
 CASE TG_TABLE_NAME
 WHEN 'finance_accounts' THEN permitted:=ARRAY['Status','Version'];
 WHEN 'finance_authorities' THEN permitted:=ARRAY['Permissions','Version'];
 WHEN 'finance_policies' THEN
  permitted:=ARRAY['Status'];
  IF NOT(old_status='Published' AND new_status='Retired') THEN RAISE EXCEPTION 'Published policies may only retire, never change their historical rules' USING ERRCODE='23514'; END IF;
 WHEN 'finance_fee_schedules' THEN
  permitted:=ARRAY['Status','Version'];
  IF old_status<>new_status AND NOT(old_status='Draft' AND new_status='Published' OR old_status='Published' AND new_status='Retired') THEN RAISE EXCEPTION 'Invalid fee schedule transition' USING ERRCODE='23514'; END IF;
 WHEN 'finance_invoices' THEN
  IF old_status='Draft' AND new_status='Draft' THEN RETURN NEW; END IF;
  permitted:=ARRAY['Status','Version','PostedAtUtc'];
  IF old_status<>new_status AND NOT(old_status='Draft' AND new_status='Posted' OR old_status='Posted' AND new_status='Reversed' OR old_status='Reversed' AND new_status='Posted') THEN RAISE EXCEPTION 'Invalid invoice transition' USING ERRCODE='23514'; END IF;
  IF OLD."PostedAtUtc" IS NOT NULL AND NEW."PostedAtUtc" IS DISTINCT FROM OLD."PostedAtUtc" THEN RAISE EXCEPTION 'Posting time is immutable' USING ERRCODE='23514'; END IF;
 WHEN 'finance_payments' THEN
  permitted:=ARRAY['Status','Version','VerifiedBy','PostedAtUtc'];
  IF old_status<>new_status AND NOT(old_status='Pending' AND new_status IN ('Verified','Rejected','Posted') OR old_status='Verified' AND new_status IN ('Posted','Rejected') OR old_status='Posted' AND new_status='Reversed') THEN RAISE EXCEPTION 'Invalid payment transition' USING ERRCODE='23514'; END IF;
  IF old_status IN ('Posted','Reversed') THEN permitted:=ARRAY['Status','Version']; END IF;
 WHEN 'finance_refunds' THEN
  permitted:=ARRAY['Status','Version','ReviewedBy','ExecutionReference'];
  IF old_status<>new_status AND NOT(old_status='Requested' AND new_status IN ('Approved','Rejected') OR old_status='Approved' AND new_status IN ('Executed','Rejected') OR old_status='Executed' AND new_status='Reversed') THEN RAISE EXCEPTION 'Invalid refund transition' USING ERRCODE='23514'; END IF;
  IF old_status IN ('Executed','Reversed') THEN permitted:=ARRAY['Status','Version']; END IF;
 WHEN 'finance_payment_plans' THEN
  permitted:=ARRAY['Status','Version','ReviewedBy','ApprovedAtUtc','SupersededAtUtc'];
  IF old_status<>new_status AND NOT(old_status='Proposed' AND new_status IN ('Approved','Rejected') OR old_status='Approved' AND new_status='Superseded') THEN RAISE EXCEPTION 'Invalid payment plan transition' USING ERRCODE='23514'; END IF;
  IF old_status='Approved' THEN permitted:=ARRAY['Status','Version','SupersededAtUtc']; END IF;
 WHEN 'finance_awards' THEN
  permitted:=ARRAY['Status','Version','ReviewedBy','ApprovalEvidenceJson'];
  IF old_status<>new_status AND NOT(old_status='Requested' AND new_status IN ('Approved','Rejected') OR old_status='Approved' AND new_status='Revoked') THEN RAISE EXCEPTION 'Invalid award transition' USING ERRCODE='23514'; END IF;
  IF old_status IN ('Approved','Revoked') THEN permitted:=ARRAY['Status','Version']; END IF;
 WHEN 'finance_holds' THEN
  permitted:=ARRAY['Status','ReleasedAtUtc'];
  IF NOT(old_status='Open' AND new_status='Released') THEN RAISE EXCEPTION 'Holds are released, never silently rewritten' USING ERRCODE='23514'; END IF;
 ELSE RAISE EXCEPTION 'Unsupported finance state table';
 END CASE;
 IF (old_json-permitted) IS DISTINCT FROM (new_json-permitted) THEN RAISE EXCEPTION 'Financial source amounts, ownership, policy and approval evidence are immutable' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;

CREATE FUNCTION p21_child_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE parent_id uuid; parent_status text;
BEGIN
 IF TG_TABLE_NAME='finance_fee_items' THEN
  IF TG_OP='DELETE' THEN parent_id:=OLD."ScheduleId"; ELSE parent_id:=NEW."ScheduleId"; END IF;
  SELECT "Status" INTO parent_status FROM finance_fee_schedules WHERE "Id"=parent_id;
 ELSE
  IF TG_OP='DELETE' THEN parent_id:=OLD."InvoiceId"; ELSE parent_id:=NEW."InvoiceId"; END IF;
  SELECT "Status" INTO parent_status FROM finance_invoices WHERE "Id"=parent_id;
 END IF;
 IF parent_status IS DISTINCT FROM 'Draft' THEN RAISE EXCEPTION 'Published pricing and posted invoice lines are immutable' USING ERRCODE='23514'; END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF; RETURN NEW;
END $$;

CREATE FUNCTION p21_validate_journal() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE journal_id uuid; j finance_journals%ROWTYPE; debit_bucket text; credit_bucket text; n integer; total numeric; positive_count integer; negative_count integer;
BEGIN
 IF TG_TABLE_NAME='finance_journals' THEN journal_id:=NEW."Id"; ELSE journal_id:=NEW."JournalId"; END IF;
 SELECT * INTO j FROM finance_journals WHERE "Id"=journal_id;
 IF NOT FOUND THEN RAISE EXCEPTION 'Journal header required' USING ERRCODE='23514'; END IF;
 SELECT count(*),COALESCE(sum("SignedMinor"),0) INTO n,total FROM finance_journal_lines WHERE "JournalId"=j."Id";
 IF n<>2 OR total<>0 THEN RAISE EXCEPTION 'An incomplete or unbalanced journal cannot commit' USING ERRCODE='23514'; END IF;
 debit_bucket:=CASE j."Kind" WHEN 'InvoicePosted' THEN 'Receivable' WHEN 'DebitAdjustment' THEN 'Receivable' WHEN 'CreditNote' THEN 'Revenue'
  WHEN 'PaymentPosted' THEN 'Funds' WHEN 'PaymentReversed' THEN 'StudentCredit' WHEN 'Allocation' THEN 'StudentCredit' WHEN 'AllocationReversal' THEN 'Receivable'
  WHEN 'ScholarshipGranted' THEN 'Aid' WHEN 'ScholarshipReclaimed' THEN 'Receivable' WHEN 'RefundExecuted' THEN 'StudentCredit' WHEN 'RefundReversed' THEN 'Funds' END;
 credit_bucket:=CASE j."Kind" WHEN 'InvoicePosted' THEN 'Revenue' WHEN 'DebitAdjustment' THEN 'Revenue' WHEN 'CreditNote' THEN 'Receivable'
  WHEN 'PaymentPosted' THEN 'StudentCredit' WHEN 'PaymentReversed' THEN 'Funds' WHEN 'Allocation' THEN 'Receivable' WHEN 'AllocationReversal' THEN 'StudentCredit'
  WHEN 'ScholarshipGranted' THEN 'Receivable' WHEN 'ScholarshipReclaimed' THEN 'Aid' WHEN 'RefundExecuted' THEN 'Funds' WHEN 'RefundReversed' THEN 'StudentCredit' END;
 SELECT count(*) FILTER(WHERE "Bucket"=debit_bucket AND "SignedMinor"=j."AmountMinor"),count(*) FILTER(WHERE "Bucket"=credit_bucket AND "SignedMinor"=-j."AmountMinor")
 INTO positive_count,negative_count FROM finance_journal_lines WHERE "JournalId"=j."Id";
 IF debit_bucket IS NULL OR positive_count<>1 OR negative_count<>1 THEN RAISE EXCEPTION 'Journal accounts/amounts do not match its financial operation' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_accounts a WHERE a."Id"=j."AccountId" AND a."StudentId"=j."ActorId") THEN RAISE EXCEPTION 'Students cannot post to their own account' USING ERRCODE='23514'; END IF;
 IF j."Kind"='InvoicePosted' AND NOT EXISTS(SELECT 1 FROM finance_invoices i WHERE i."Id"=j."SourceId" AND i."Id"=j."InvoiceId" AND i."AccountId"=j."AccountId" AND i."TotalMinor"=j."AmountMinor" AND i."Status" IN ('Posted','Reversed'))
 THEN RAISE EXCEPTION 'Invoice posting source mismatch' USING ERRCODE='23514'; END IF;
 IF j."Kind" IN ('PaymentPosted','PaymentReversed') AND NOT EXISTS(SELECT 1 FROM finance_payments p JOIN finance_receipts r ON r."PaymentId"=p."Id" WHERE p."Id"=j."SourceId" AND p."Id"=j."PaymentId" AND p."AccountId"=j."AccountId" AND p."AmountMinor"=j."AmountMinor" AND p."Status" IN ('Posted','Reversed'))
 THEN RAISE EXCEPTION 'Posted payment and receipt source required' USING ERRCODE='23514'; END IF;
 IF j."Kind" IN ('Allocation','AllocationReversal') AND NOT EXISTS(SELECT 1 FROM finance_allocations a WHERE a."Id"=j."SourceId" AND a."JournalId"=j."Id" AND a."InvoiceId"=j."InvoiceId" AND a."PaymentId"=j."PaymentId" AND a."AccountId"=j."AccountId" AND a."SignedMinor"=CASE WHEN j."Kind"='Allocation' THEN j."AmountMinor" ELSE -j."AmountMinor" END)
 THEN RAISE EXCEPTION 'Allocation journal source mismatch' USING ERRCODE='23514'; END IF;
 IF j."Kind" IN ('DebitAdjustment','CreditNote') AND NOT EXISTS(SELECT 1 FROM finance_adjustments a WHERE a."JournalId"=j."Id" AND a."InvoiceId"=j."InvoiceId" AND a."SignedMinor"=CASE WHEN j."Kind"='DebitAdjustment' THEN j."AmountMinor" ELSE -j."AmountMinor" END)
 THEN RAISE EXCEPTION 'Adjustment journal source mismatch' USING ERRCODE='23514'; END IF;
 IF j."Kind" IN ('ScholarshipGranted','ScholarshipReclaimed') AND NOT EXISTS(SELECT 1 FROM finance_awards a JOIN finance_scholarships s ON s."Id"=a."ScholarshipId" WHERE a."Id"=j."SourceId" AND a."InvoiceId"=j."InvoiceId" AND a."Status" IN ('Approved','Revoked') AND s."Currency"=j."Currency")
 THEN RAISE EXCEPTION 'Approved scholarship source required' USING ERRCODE='23514'; END IF;
 IF j."Kind" IN ('RefundExecuted','RefundReversed') AND NOT EXISTS(SELECT 1 FROM finance_refunds r WHERE r."Id"=j."SourceId" AND r."AccountId"=j."AccountId" AND r."PaymentId"=j."PaymentId" AND r."AmountMinor"=j."AmountMinor" AND r."Status" IN ('Executed','Reversed') AND r."ExecutionReference" IS NOT NULL)
 THEN RAISE EXCEPTION 'Executed refund source required' USING ERRCODE='23514'; END IF;
 IF j."Kind" IN ('PaymentReversed','RefundReversed') AND NOT EXISTS(SELECT 1 FROM finance_journals old WHERE old."Id"=j."ReversesJournalId" AND old."AccountId"=j."AccountId" AND old."Currency"=j."Currency" AND old."SourceId"=j."SourceId" AND old."AmountMinor"=j."AmountMinor" AND old."Kind"=CASE WHEN j."Kind"='PaymentReversed' THEN 'PaymentPosted' ELSE 'RefundExecuted' END)
 THEN RAISE EXCEPTION 'A reversal must correct its matching original journal once' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;

CREATE FUNCTION p21_validate_account() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE account_id uuid; ar numeric; funds numeric; credit numeric; gross numeric; aid numeric; allocated numeric; cash numeric; refunds numeric; expected_ar numeric;
BEGIN
 IF TG_TABLE_NAME='finance_journal_lines' THEN SELECT "AccountId" INTO account_id FROM finance_journals WHERE "Id"=NEW."JournalId";
 ELSIF TG_TABLE_NAME IN ('finance_invoice_lines','finance_adjustments','finance_awards','finance_payment_plans') THEN SELECT "AccountId" INTO account_id FROM finance_invoices WHERE "Id"=(to_jsonb(NEW)->>'InvoiceId')::uuid;
 ELSIF TG_TABLE_NAME='finance_installments' THEN SELECT i."AccountId" INTO account_id FROM finance_payment_plans p JOIN finance_invoices i ON i."Id"=p."InvoiceId" WHERE p."Id"=NEW."PlanId";
 ELSIF TG_TABLE_NAME='finance_receipts' THEN SELECT "AccountId" INTO account_id FROM finance_payments WHERE "Id"=NEW."PaymentId";
 ELSE account_id:=(to_jsonb(NEW)->>'AccountId')::uuid; END IF;
 IF account_id IS NULL THEN RAISE EXCEPTION 'Financial account linkage required' USING ERRCODE='23514'; END IF;
 PERFORM 1 FROM finance_accounts WHERE "Id"=account_id FOR UPDATE;
 SELECT COALESCE(sum(l."SignedMinor") FILTER(WHERE l."Bucket"='Receivable'),0),COALESCE(sum(l."SignedMinor") FILTER(WHERE l."Bucket"='Funds'),0),COALESCE(sum(l."SignedMinor") FILTER(WHERE l."Bucket"='StudentCredit'),0)
 INTO ar,funds,credit FROM finance_journal_lines l JOIN finance_journals j ON j."Id"=l."JournalId" WHERE j."AccountId"=account_id;
 IF ar<0 OR funds<0 OR credit>0 OR greatest(abs(ar),abs(funds),abs(credit))>9000000000000000 THEN RAISE EXCEPTION 'Invalid receivable, collected funds, or student cash-credit balance' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_journal_lines l JOIN finance_journals j ON j."Id"=l."JournalId" WHERE j."AccountId"=account_id GROUP BY l."Bucket" HAVING abs(sum(l."SignedMinor"))>9000000000000000)
 THEN RAISE EXCEPTION 'Financial account balance exceeds supported bounds' USING ERRCODE='23514'; END IF;
 SELECT COALESCE(sum("TotalMinor"),0) INTO gross FROM finance_invoices WHERE "AccountId"=account_id AND "Status"<>'Draft';
 SELECT gross+COALESCE(sum(a."SignedMinor"),0) INTO gross FROM finance_adjustments a JOIN finance_invoices i ON i."Id"=a."InvoiceId" WHERE i."AccountId"=account_id;
 SELECT COALESCE(sum(CASE WHEN "Kind"='ScholarshipGranted' THEN "AmountMinor" ELSE -"AmountMinor" END),0) INTO aid FROM finance_journals WHERE "AccountId"=account_id AND "Kind" IN ('ScholarshipGranted','ScholarshipReclaimed');
 SELECT COALESCE(sum("SignedMinor"),0) INTO allocated FROM finance_allocations WHERE "AccountId"=account_id;
 SELECT COALESCE(sum("AmountMinor"),0) INTO cash FROM finance_payments WHERE "AccountId"=account_id AND "Status"='Posted';
 SELECT COALESCE(sum("AmountMinor"),0) INTO refunds FROM finance_refunds WHERE "AccountId"=account_id AND "Status"='Executed';
 expected_ar:=gross-aid-allocated;
 IF ar<>expected_ar OR funds<>cash-refunds OR -credit<>cash-allocated-refunds THEN RAISE EXCEPTION 'Student account ledger does not reconcile to invoice, allocation, posted payment and refund records' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_invoices i WHERE i."AccountId"=account_id AND
     (i."TotalMinor"<>(SELECT COALESCE(sum(l."AmountMinor"),0) FROM finance_invoice_lines l WHERE l."InvoiceId"=i."Id") OR
      (i."Status"<>'Draft' AND i."TotalMinor"+COALESCE((SELECT sum(a."SignedMinor") FROM finance_adjustments a WHERE a."InvoiceId"=i."Id"),0)-
       COALESCE((SELECT sum(CASE WHEN j."Kind"='ScholarshipGranted' THEN j."AmountMinor" ELSE -j."AmountMinor" END) FROM finance_journals j WHERE j."InvoiceId"=i."Id" AND j."Kind" IN ('ScholarshipGranted','ScholarshipReclaimed')),0)-
       COALESCE((SELECT sum(a."SignedMinor") FROM finance_allocations a WHERE a."InvoiceId"=i."Id"),0)<0)))
 THEN RAISE EXCEPTION 'Invoice lines or outstanding liability do not reconcile' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_invoice_lines l JOIN finance_invoices i ON i."Id"=l."InvoiceId" WHERE i."AccountId"=account_id AND l."AmountMinor"+COALESCE((SELECT sum(a."SignedMinor") FROM finance_adjustments a WHERE a."InvoiceLineId"=l."Id"),0)<0)
 THEN RAISE EXCEPTION 'Correcting credits exceed the eligible charge' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_payments p WHERE p."AccountId"=account_id AND
   COALESCE((SELECT sum(a."SignedMinor") FROM finance_allocations a WHERE a."PaymentId"=p."Id"),0)+
   COALESCE((SELECT sum(r."AmountMinor") FROM finance_refunds r WHERE r."PaymentId"=p."Id" AND r."Status" IN ('Requested','Approved','Executed')),0)>
   CASE WHEN p."Status"='Posted' THEN p."AmountMinor" ELSE 0 END)
 THEN RAISE EXCEPTION 'Cash was allocated or reserved/refunded more than once' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_allocations a LEFT JOIN finance_allocations original ON original."Id"=a."OriginalAllocationId" JOIN finance_journals j ON j."Id"=a."JournalId"
   WHERE a."AccountId"=account_id AND (a."InvoiceId" IS DISTINCT FROM j."InvoiceId" OR a."PaymentId" IS DISTINCT FROM j."PaymentId" OR a."AccountId"<>j."AccountId" OR
     a."SignedMinor"<>CASE WHEN j."Kind"='Allocation' THEN j."AmountMinor" WHEN j."Kind"='AllocationReversal' THEN -j."AmountMinor" ELSE 0 END OR
     a."SignedMinor"<0 AND (original."SignedMinor"<=0 OR original."InvoiceId"<>a."InvoiceId" OR original."PaymentId"<>a."PaymentId")))
 THEN RAISE EXCEPTION 'Allocation source/reversal lineage mismatch' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_allocations a WHERE a."AccountId"=account_id AND a."SignedMinor">0 AND a."SignedMinor"+COALESCE((SELECT sum(r."SignedMinor") FROM finance_allocations r WHERE r."OriginalAllocationId"=a."Id"),0)<0)
 THEN RAISE EXCEPTION 'Allocation reversed more than its original amount' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_payment_plans p JOIN finance_invoices i ON i."Id"=p."InvoiceId" WHERE i."AccountId"=account_id AND
   p."BaseDebtMinor"<>(SELECT COALESCE(sum(s."AmountMinor"),0) FROM finance_installments s WHERE s."PlanId"=p."Id"))
 THEN RAISE EXCEPTION 'Installment obligations do not reconcile to the frozen debt snapshot' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_awards a JOIN finance_invoices i ON i."Id"=a."InvoiceId" JOIN finance_accounts ac ON ac."Id"=i."AccountId" WHERE i."AccountId"=account_id AND a."Status" IN ('Approved','Revoked') AND a."ReviewedBy"=ac."StudentId")
 THEN RAISE EXCEPTION 'Student self-approved financial assistance' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_awards a JOIN finance_invoices i ON i."Id"=a."InvoiceId" WHERE i."AccountId"=account_id AND
    ((SELECT COALESCE(sum(CASE WHEN j."Kind"='ScholarshipGranted' THEN j."AmountMinor" ELSE -j."AmountMinor" END),0) FROM finance_journals j WHERE j."SourceId"=a."Id" AND j."Kind" IN ('ScholarshipGranted','ScholarshipReclaimed'))<0 OR
     a."Status"<>'Approved' AND (SELECT COALESCE(sum(CASE WHEN j."Kind"='ScholarshipGranted' THEN j."AmountMinor" ELSE -j."AmountMinor" END),0) FROM finance_journals j WHERE j."SourceId"=a."Id" AND j."Kind" IN ('ScholarshipGranted','ScholarshipReclaimed'))<>0))
 THEN RAISE EXCEPTION 'Scholarship correcting entries exceed their approved source or leave revoked assistance applied' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_scholarships s WHERE s."FundingLimitMinor" IS NOT NULL AND EXISTS(SELECT 1 FROM finance_awards a JOIN finance_invoices i ON i."Id"=a."InvoiceId" WHERE a."ScholarshipId"=s."Id" AND i."AccountId"=account_id) AND
   (SELECT COALESCE(sum(CASE WHEN j."Kind"='ScholarshipGranted' THEN j."AmountMinor" ELSE -j."AmountMinor" END),0) FROM finance_journals j JOIN finance_awards a ON a."Id"=j."SourceId" WHERE a."ScholarshipId"=s."Id" AND j."Kind" IN ('ScholarshipGranted','ScholarshipReclaimed'))>s."FundingLimitMinor")
 THEN RAISE EXCEPTION 'Scholarship funding limit exceeded' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM finance_holds h JOIN finance_invoices i ON i."Id"=h."InvoiceId" WHERE h."AccountId"=account_id AND (i."AccountId"<>h."AccountId" OR i."PolicyId"<>h."PolicyId"))
 THEN RAISE EXCEPTION 'Financial hold source mismatch' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;

DO $$ DECLARE table_name text; BEGIN
 FOREACH table_name IN ARRAY ARRAY['finance_journals','finance_journal_lines','finance_adjustments','finance_allocations','finance_receipts','finance_commands','finance_audit_events','finance_reconciliations','finance_installments','finance_scholarships'] LOOP
  EXECUTE format('CREATE TRIGGER p21_immutable BEFORE UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION p21_immutable()',table_name);
 END LOOP;
 FOREACH table_name IN ARRAY ARRAY['finance_accounts','finance_authorities','finance_policies','finance_fee_schedules','finance_invoices','finance_payments','finance_refunds','finance_payment_plans','finance_awards','finance_holds'] LOOP
  EXECUTE format('CREATE TRIGGER p21_state BEFORE UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION p21_state_guard()',table_name);
 END LOOP;
 FOREACH table_name IN ARRAY ARRAY['finance_fee_items','finance_invoice_lines'] LOOP
  EXECUTE format('CREATE TRIGGER p21_child BEFORE INSERT OR UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION p21_child_guard()',table_name);
 END LOOP;
 FOREACH table_name IN ARRAY ARRAY['finance_journals','finance_journal_lines'] LOOP
  EXECUTE format('CREATE CONSTRAINT TRIGGER p21_balanced AFTER INSERT ON %I DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION p21_validate_journal()',table_name);
 END LOOP;
 FOREACH table_name IN ARRAY ARRAY['finance_journals','finance_journal_lines','finance_invoices','finance_invoice_lines','finance_adjustments','finance_allocations','finance_payments','finance_refunds','finance_receipts','finance_awards','finance_payment_plans','finance_installments','finance_holds'] LOOP
  EXECUTE format('CREATE CONSTRAINT TRIGGER p21_reconciled AFTER INSERT OR UPDATE ON %I DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION p21_validate_account()',table_name);
 END LOOP;
END $$;
CREATE TRIGGER p21_account_scope BEFORE INSERT OR UPDATE ON finance_accounts FOR EACH ROW EXECUTE FUNCTION p21_user_scope('StudentId');
CREATE TRIGGER p21_authority_scope BEFORE INSERT OR UPDATE ON finance_authorities FOR EACH ROW EXECUTE FUNCTION p21_user_scope('UserId');
CREATE TRIGGER p21_journal_scope BEFORE INSERT ON finance_journals FOR EACH ROW EXECUTE FUNCTION p21_user_scope('ActorId');
CREATE TRIGGER p21_payment_scope BEFORE INSERT OR UPDATE ON finance_payments FOR EACH ROW EXECUTE FUNCTION p21_user_scope('RegisteredBy','VerifiedBy');
CREATE TRIGGER p21_refund_scope BEFORE INSERT OR UPDATE ON finance_refunds FOR EACH ROW EXECUTE FUNCTION p21_user_scope('RequestedBy','ReviewedBy');
CREATE TRIGGER p21_plan_scope BEFORE INSERT OR UPDATE ON finance_payment_plans FOR EACH ROW EXECUTE FUNCTION p21_user_scope('RequestedBy','ReviewedBy');
CREATE TRIGGER p21_award_scope BEFORE INSERT OR UPDATE ON finance_awards FOR EACH ROW EXECUTE FUNCTION p21_user_scope('RequestedBy','ReviewedBy');
CREATE TRIGGER p21_audit_scope BEFORE INSERT ON finance_audit_events FOR EACH ROW EXECUTE FUNCTION p21_user_scope('ActorId');
CREATE TRIGGER p21_reconciliation_scope BEFORE INSERT ON finance_reconciliations FOR EACH ROW EXECUTE FUNCTION p21_user_scope('ActorId');

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261102100000_Phase21StudentFinance', '10.0.3');

COMMIT;

