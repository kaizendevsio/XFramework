-- Explicit QA-only fixture. No provider endpoint, secrets, or external payment connection.
-- Run with psql ON_ERROR_STOP=1 against Xeon Dev; existing records are never overwritten.
BEGIN;
INSERT INTO "Integration.PaymentGateway"."GatewayCategory"
    ("ID", "Name", "Description", "TenantId", "ConcurrencyStamp", "CreatedAt")
VALUES
    ('ac38e692-383b-4386-8a31-53e043577025', 'QA manual settlement',
     'Isolated ledger verification only; no external payments',
     '72099247-9d0d-4290-8364-29feb88a422b', uuid_generate_v4(), now())
ON CONFLICT ("ID") DO NOTHING;

INSERT INTO "Integration.PaymentGateway"."Gateway"
    ("ID", "GatewayCategoryID", "Name", "Description", "ServiceCharge",
     "ConvenienceFee", "TenantId", "ConcurrencyStamp", "CreatedAt")
VALUES
    ('d3514563-681e-48b4-8ab1-1182a2b8dd80', 'ac38e692-383b-4386-8a31-53e043577025',
     'QA Manual Settlement', 'QA ledger verification only. No external payment is sent.',
     0, 0, '72099247-9d0d-4290-8364-29feb88a422b', uuid_generate_v4(), now())
ON CONFLICT ("ID") DO NOTHING;
COMMIT;
