# TaxApiSample

A .NET 10 console application that calls the CCH Axcess **Tax Services v2**
API (`Swagger/tsv2.json`) using a session token obtained from the
**Authentication API** (`Swagger/Auth.json`).

## Setup

1. Copy `.env.example` to `.env` and fill in your values:
   ```
   INTEGRATOR_KEY=your-integrator-or-subscription-key
   CCH_USERNAME=your-username
   CCH_PASSWORD=your-password
   CCH_USER_SID=
   CCH_REALM=
   ```
   `.env` is gitignored and must never be committed.

2. Base URLs live in `appsettings.json` (not secret), one entry per API:
   ```json
   {
     "CchTaxApi": {
       "AuthBaseUrl": "https://test4api.cchaxcess.com/api/AuthService",
       "TaxServiceBaseUrl": "https://test4api.cchaxcess.com/taxservices/oiptax",
       "UseAzureAuthenticate": false
     }
   }
   ```
   Point these at whichever CCH Axcess environment/server you use. Set
   `UseAzureAuthenticate` to `true` if your firm uses Azure AD authentication
   instead of the CCH Axcess login method.

## Usage

```
$> TaxApiSample login
$> TaxApiSample logout
$> TaxApiSample list
$> TaxApiSample <endpoint-name> [--method GET|POST|PUT|DELETE]
                                 [--query key=value ...]
                                 [--body '<json>' | --body @file.json]
                                 [--security <token>] [--authorization <token>]
                                 [--integrator-key <key>]
```

- `login` authenticates against the Authentication API using the credentials
  in `.env` and caches the returned session token under
  `%LOCALAPPDATA%\TaxApiSample\token.json` for reuse by later commands.
- `list` prints every Tax Services v2 endpoint (parsed directly from the
  embedded `tsv2.json`), its HTTP methods, query parameters, and whether it
  requires a request body.
- `<endpoint-name>` is the last path segment of a Tax Services v2 operation,
  e.g. `Returns`, `CalculateReturn`, `BatchStatus`, `ReturnSections`. The
  method is inferred automatically when the endpoint only supports one.

### Examples

```
$> TaxApiSample login
$> TaxApiSample Returns --query "$filter=TaxYear eq '2023'" --query "$orderby=ReturnClientName"
$> TaxApiSample CalculateReturn --body @calculate-request.json
$> TaxApiSample Returns --method DELETE --body '{"ReturnId":["2021I:TEST1:V1"],"RecoveryCopies":false,"K1ImportFiles":false}'
```

> PowerShell note: wrap query strings containing `$filter`/`$orderby` in
> **single** quotes, or escape the `$`, to avoid variable expansion.

Every request automatically includes the `IntegratorKey` header (from
`.env`) and either the `Security` header (cached session token from `login`)
or an `Authorization: Bearer <token>` header (via `--authorization`, for
firms using OAuth 2.0).
