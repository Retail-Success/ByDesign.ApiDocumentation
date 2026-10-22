# ByDesign API

The ByDesign API lets partners and integrations read and write ByDesign data, including inventory, orders, payments, customers, reps, parties and payouts, over HTTPS using JSON.

This repository contains everything you need to start calling the API:

| What | Where | Use it to |
| --- | --- | --- |
| OpenAPI specification | [`bydesign-client-api-v2.openapi.json`](bydesign-client-api-v2.openapi.json) | Browse every route and model, or generate a typed client in your language |
| Bruno collection | [`Bruno/`](Bruno) | Try every route interactively without writing code |
| Code samples | [`samples/`](samples) | Copy a working token-and-request flow into your own project |

## Contents

- [Quick start](#quick-start)
- [Get your credentials](#get-your-credentials)
- [Authentication](#authentication)
- [Making requests](#making-requests)
- [Errors and rate limits](#errors-and-rate-limits)
- [Generate a client from the OpenAPI spec](#generate-a-client-from-the-openapi-spec)
- [Try the API with Bruno](#try-the-api-with-bruno)
- [Code samples](#code-samples)
- [Endpoint reference](#endpoint-reference)
- [Security checklist](#security-checklist)

## Quick start

1. In the ByDesign back office, generate a **client ID**, **client secret** and **API key**, and copy your **API URL**. See [Get your credentials](#get-your-credentials).
2. Exchange your client ID and secret for an access token:

   ```bash
   curl -X POST "$BYDESIGN_API_URL/oauth2/token" \
     -H "Content-Type: application/x-www-form-urlencoded" \
     -d "grant_type=client_credentials" \
     -d "client_id=$BYDESIGN_CLIENT_ID" \
     -d "client_secret=$BYDESIGN_CLIENT_SECRET" \
     -d "scope=api/Read api/Write"
   ```

3. Call the API with the token and your API key:

   ```bash
   curl "$BYDESIGN_API_URL/api/v2/inventory/categorydetail" \
     -H "Authorization: Bearer $ACCESS_TOKEN" \
     -H "x-api-key: $BYDESIGN_API_KEY"
   ```

## Get your credentials

You create and manage your API credentials yourself in the **ByDesign back office**. Sign in and open the API credentials area to:

1. **Generate a client ID and client secret.** These are your OAuth 2.0 credentials. You exchange them for a short-lived access token.
2. **Generate an API key.** Send it in the `x-api-key` header on every request. It identifies your integration and links your traffic to your rate limits and request quota.
3. **Copy your API URL.** Your company has its own API URL. Every route and the token endpoint are served from it, so it's the only base address you need to configure.

> [!IMPORTANT]
> Store your client secret and API key in a secrets manager or other secure configuration store as soon as you generate them. If either is lost or exposed, generate a new one in the back office and update your integration.

The examples in this repository read these values from environment variables:

| Variable | Value |
| --- | --- |
| `BYDESIGN_API_URL` | Your API URL from the back office, for example `https://your-api-url` (no trailing slash) |
| `BYDESIGN_CLIENT_ID` | Your client ID |
| `BYDESIGN_CLIENT_SECRET` | Your client secret |
| `BYDESIGN_API_KEY` | Your API key |

## Authentication

Every API request needs two headers:

```http
Authorization: Bearer {access_token}
x-api-key: {api_key}
```

### Request an access token

The API uses the OAuth 2.0 **client credentials** flow. Send a `POST` to `/oauth2/token` on your API URL with a form-encoded body:

```http
POST {api_url}/oauth2/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id={client_id}&client_secret={client_secret}&scope=api/Read api/Write
```

| Field | Required | Value |
| --- | --- | --- |
| `grant_type` | Yes | Always `client_credentials` |
| `client_id` | Yes | Your client ID |
| `client_secret` | Yes | Your client secret |
| `scope` | No | Space-separated scopes to request. If you leave it out, the token gets every scope your credentials allow. |

Response:

```json
{
  "access_token": "eyJraWQiOi...",
  "expires_in": 3600,
  "token_type": "Bearer"
}
```

### Scopes

| Scope | Grants |
| --- | --- |
| `api/Read` | All `GET` routes |
| `api/Write` | All `POST`, `PUT`, `PATCH` and `DELETE` routes |

Request only the scopes you need. Read-only credentials receive `api/Read` only, and any write request made with them is rejected with `403 Forbidden`.

### Reuse your token

Tokens are valid for one hour (`expires_in` is in seconds). Request a token once, cache it, and reuse it until shortly before it expires. **Don't request a new token for every API call.** If a request returns `401 Unauthorized`, get a new token and retry once. Every [code sample](#code-samples) except the curl and PowerShell scripts does this for you.

The API key doesn't expire. Send it unchanged on every request.

## Making requests

- **Base URL:** every route is relative to your API URL. For example, `GET /api/v2/customer/{id}` is `GET {api_url}/api/v2/customer/{id}`.
- **Path parameters:** replace segments such as `{orderid}` or `{repdid}` with real values, URL-encoded.
- **Query parameters:** optional filters and paging, such as `pageSize` and `pageNumber` on inventory detail, are passed as query strings.
- **Request bodies:** send JSON with `Content-Type: application/json`. Each route's body model is defined in the [OpenAPI spec](bydesign-client-api-v2.openapi.json).
- **Dates:** send timestamps in UTC unless a route says otherwise.
- **Server-side only:** call the API from your servers, never from browser or mobile code, because those can't keep your client secret and API key private.

## Errors and rate limits

| Status | Meaning | What to do |
| --- | --- | --- |
| `400 Bad Request` | The request body or parameters are invalid | Check the request against the OpenAPI spec |
| `401 Unauthorized` | The Bearer token is missing, malformed or expired | Request a new token and retry |
| `403 Forbidden` | The token lacks the required scope (for example, an `api/Read` token on a write route), or the API key is missing or invalid | Request the right scope, or check your API key |
| `404 Not Found` | The resource doesn't exist | Check the ID or path |
| `429 Too Many Requests` | You've hit your rate limit or request quota | Back off and retry with exponential delay |

Your API key's usage plan sets your request rate, burst and monthly quota limits. Build `429` handling into your integration from the start. The code samples retry with exponential backoff.

## Generate a client from the OpenAPI spec

[`bydesign-client-api-v2.openapi.json`](bydesign-client-api-v2.openapi.json) is a complete OpenAPI 3.0 description of the API: every route, parameter, request body, response model and security requirement. Rather than hand-writing request and response classes, you can feed it to a code generator to get a typed client in your language.

Popular generators:

| Generator | Languages | Example |
| --- | --- | --- |
| [OpenAPI Generator](https://openapi-generator.tech) | 50+ including C#, Java, Python, TypeScript, Go, PHP, Ruby | `npx @openapitools/openapi-generator-cli generate -i bydesign-client-api-v2.openapi.json -g csharp -o ./ByDesignClient` |
| [Kiota](https://learn.microsoft.com/openapi/kiota/) | C#, Java, Go, Python, TypeScript, PHP, Ruby | `kiota generate -l CSharp -d bydesign-client-api-v2.openapi.json -c ByDesignApiClient -n ByDesign.Api -o ./ByDesignClient` |
| [NSwag](https://github.com/RicoSuter/NSwag) | C#, TypeScript | `nswag openapi2csclient /input:bydesign-client-api-v2.openapi.json /namespace:ByDesign.Api /output:ByDesignClient.cs` |
| [openapi-typescript](https://openapi-ts.dev) | TypeScript types | `npx openapi-typescript bydesign-client-api-v2.openapi.json -o bydesign-api.d.ts` |
| [openapi-python-client](https://github.com/openapi-generators/openapi-python-client) | Python | `openapi-python-client generate --path bydesign-client-api-v2.openapi.json` |

When you use a generated client:

- **Set the base URL** to your API URL. The spec's server URL, `https://{client-domain}`, is a placeholder.
- **Add both headers to every request.** Most generators let you plug in an authentication handler or HTTP middleware. Use it to attach the cached `Authorization: Bearer` token and the `x-api-key` header. The [code samples](#code-samples) show token caching you can reuse.

You can also open the spec in [Swagger Editor](https://editor.swagger.io), [Redocly](https://redocly.github.io/redoc/) or [Bruno](#try-the-api-with-bruno) to browse it as interactive documentation.

## Try the API with Bruno

[Bruno](https://www.usebruno.com) is a free, open-source API client. The [`Bruno/`](Bruno) folder holds a ready-made collection with a request for every route, grouped by area.

1. [Download and install Bruno](https://www.usebruno.com/downloads).
2. Choose **Open Collection** and select the `Bruno` folder from this repository.
3. Select the **ByDesign** environment and open its settings:
   - Set `baseUrl` to your API URL.
   - Enter `clientId`, `clientSecret` and `apiKey`. These are marked **secret**, so Bruno stores them encrypted on your machine and never writes them into the collection files.
4. Run **Get Access Token**. It saves the token for the session, and every other request sends it with your API key automatically.
5. Open any request, fill in its path parameters on the **Params** tab, and send it.

Tokens last one hour. When requests start returning `401`, run **Get Access Token** again. If your credentials are read-only, remove `api/Write` from the token request's `scope`.

You can also run the collection from the command line with the [Bruno CLI](https://docs.usebruno.com/bru-cli/overview):

```bash
cd Bruno
npx @usebruno/cli run "Get Access Token.bru" "Inventory/List Inventory Categories.bru" --env ByDesign \
  --env-var baseUrl="$BYDESIGN_API_URL" \
  --env-var clientId="$BYDESIGN_CLIENT_ID" \
  --env-var clientSecret="$BYDESIGN_CLIENT_SECRET" \
  --env-var apiKey="$BYDESIGN_API_KEY"
```

## Code samples

Each sample requests a token, makes a `GET` request (list inventory categories) and a `POST` request (look up customers by email), and prints the results. All of them read their settings from the [environment variables](#get-your-credentials) above.

| Language | Sample | Requires | Run |
| --- | --- | --- | --- |
| curl | [`samples/curl/bydesign-api.sh`](samples/curl/bydesign-api.sh) | bash, curl, jq | `bash samples/curl/bydesign-api.sh` |
| PowerShell | [`samples/powershell/ByDesignApi.ps1`](samples/powershell/ByDesignApi.ps1) | PowerShell 7+ | `pwsh samples/powershell/ByDesignApi.ps1` |
| C# | [`samples/csharp/ByDesignApiSample.cs`](samples/csharp/ByDesignApiSample.cs) | .NET 10 SDK | `dotnet run samples/csharp/ByDesignApiSample.cs` |
| Python | [`samples/python/bydesign_api.py`](samples/python/bydesign_api.py) | Python 3.9+, `requests` | `python samples/python/bydesign_api.py` |
| JavaScript (Node.js) | [`samples/javascript/bydesign-api.mjs`](samples/javascript/bydesign-api.mjs) | Node.js 18+ | `node samples/javascript/bydesign-api.mjs` |
| Java | [`samples/java/ByDesignApiSample.java`](samples/java/ByDesignApiSample.java) | Java 11+ | `java samples/java/ByDesignApiSample.java` |
| PHP | [`samples/php/bydesign-api.php`](samples/php/bydesign-api.php) | PHP 8.0+ with curl | `php samples/php/bydesign-api.php` |

The C#, Python, JavaScript, Java and PHP samples each include a small reusable client class that:

- caches the access token and refreshes it a minute before it expires
- requests a new token and retries once on `401 Unauthorized`
- retries `429 Too Many Requests` with exponential backoff
- sends the `x-api-key` header on every request

The curl and PowerShell scripts are kept minimal to show the raw HTTP calls.

The C# sample also accepts its settings as command-line arguments, which take precedence over environment variables:

```bash
dotnet run samples/csharp/ByDesignApiSample.cs -- --api-url https://your-api-url --client-id ... --client-secret ... --api-key ...
```

Its `ByDesignClient` takes an `HttpClient` in its constructor, so you can register it as a typed client with `IHttpClientFactory`. Register the token cache as a singleton so every client instance shares one token:

```csharp
builder.Services.AddSingleton(new ByDesignClientOptions
{
    ApiUrl = builder.Configuration["ByDesign:ApiUrl"]!,
    ClientId = builder.Configuration["ByDesign:ClientId"]!,
    ClientSecret = builder.Configuration["ByDesign:ClientSecret"]!,
    ApiKey = builder.Configuration["ByDesign:ApiKey"]!,
});
builder.Services.AddSingleton<ByDesignTokenCache>();
builder.Services.AddHttpClient<ByDesignClient>();
```

## Endpoint reference

All routes below are relative to your API URL. `GET` routes need the `api/Read` scope and all other methods need `api/Write`. For parameters, request bodies and response models, see the [OpenAPI spec](bydesign-client-api-v2.openapi.json).

### Inventory

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/api/v2/inventory/detail/{sku}` | Return the inventory summary for a given product ID. |
| `GET` | `/api/v2/inventory/group/{sku}` | List the child items of an inventory group with their quantities. |
| `GET` | `/api/v2/inventory/category/{sku}` | List inventory categories for display, filtered by rep/customer, locale and cart configuration. |
| `GET` | `/api/v2/inventory/categorydetail` | List all inventory categories with full detail. |
| `GET` | `/api/v2/inventory/categorydetail/{categoryid}` | Get a specific inventory category by ID. |
| `GET` | `/api/v2/inventory/media/{productid}` | Return the media (images and video) for an inventory item. |
| `GET` | `/api/v2/inventory/price/{productid}/{rankpricetypeid}` | Get the price and rank-price values for an inventory item. |
| `GET` | `/api/v2/inventory/shopping` | List the catalog products a rep or customer can buy, with pricing, stock messages and images, or get one product by `productID`. Supports OData query options. |
| `GET` | `/api/v2/inventory/review/{productid}` | List the approved reviews for a product. |
| `GET` | `/api/v2/inventory/warehouse/{productid}` | List a product's stock levels in each warehouse. |
| `GET` | `/api/v2/inventory/collection/{inventorycollectionid}` | List the products in an inventory collection that can be bought on a back-office order, priced for a rep or customer. Supports OData query options. |

### Orders

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/api/v2/order/{orderid}` | Get a specific order by ID. |
| `GET` | `/api/v2/order/{orderid}/lineitemdetails` | Return detailed line-item information for an order. |
| `GET` | `/api/v2/order/{orderid}/miscfield` | Return all misc field values for an order. |
| `GET` | `/api/v2/order/miscfieldsearch/{fieldname}/{valuetosearch}` | Search orders for a misc field value and return the matching order numbers. |
| `GET` | `/api/v2/order/ordersforrep/{repdid}` | Return the orders belonging to a rep. |
| `POST` | `/api/v2/order` | Create a new order for a rep or customer. The order is created in entered status. |
| `POST` | `/api/v2/order/workflow` | Submit an order workflow request. |
| `POST` | `/api/v2/order/returnorder/{fromorderid}` | Create a return order for an existing order. |
| `GET` | `/api/v2/order/returnorder/byorderid/{orderid}` | Get return orders by original order ID. |
| `POST` | `/api/v2/order/{orderid}/post` | Post (make official) an unofficial order. |
| `GET` | `/api/v2/order/ordersforinvoicenumber/{invoicenumber}` | Get orders matching an invoice number. |
| `POST` | `/api/v2/order/retailreceipt` | Post new retail receipts. `OrderDate` must be UTC. |
| `PUT` | `/api/v2/order/{orderid}/address` | Update an order's billing or shipping address and, by default, recalculate its taxes. |
| `POST` | `/api/v2/promotion/applysuggestedpromotion` | Apply a suggested promotion to a shopping cart or order, using the products the shopper chose for it. |

### Cart

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/api/v2/onlineorderdetail/byonlineorderid/{onlineorderid}` | List the items in a shopping cart, optionally including items added by promotions. |
| `POST` | `/api/v2/onlineorderdetail` | Add a product to a shopping cart. |
| `PUT` | `/api/v2/onlineorderdetail` | Change the quantity of a shopping cart item, optionally with price, tax and volume overrides. |
| `POST` | `/api/v2/onlineorder` | Create a shopping cart for a rep or customer, optionally adding products to it. |
| `POST` | `/api/v2/onlineorder/foronlinesignup` | Create a shopping cart for an in-progress enrollment, optionally adding products to it. |
| `POST` | `/api/v2/onlineorder/clear/{onlineorderid}` | Remove all items from a shopping cart. |
| `POST` | `/api/v2/onlineorder/latest` | Get the shopper's most recent incomplete shopping cart, or create one if none exists. |
| `POST` | `/api/v2/onlineorder/additemstolatest` | Add products to the shopper's most recent incomplete shopping cart, creating one if needed. |
| `GET` | `/api/v2/onlineorder/totals/{onlineorderid}` | Get a shopping cart's total, shipping total and handling fee after gift certificates and credits are applied. |
| `PUT` | `/api/v2/onlineorder/{onlineorderid}/shipmethod` | Change a shopping cart's shipping method and return the recalculated totals. |
| `POST` | `/api/v2/onlineorder/{onlineorderid}/shipmethodwithoverrides` | Change a shopping cart's shipping method with optional shipping, handling and tax overrides, and return the recalculated totals. |

### Payments

| Method | Path | Description |
| --- | --- | --- |
| `POST` | `/api/v2/order/payment/creditcard/save` | Record an already-processed credit card payment for an order. This doesn't charge the card. |
| `POST` | `/api/v2/order/payment/cash/save` | Add a cash payment to an order. |
| `POST` | `/api/v2/order/payment/create` | Create a payment record with default values. `OrderID` and `Amount` are required. |
| `GET` | `/api/v2/order/payment/{orderid}` | Get all payments for an order, or one payment by `paymentID`. |
| `GET` | `/api/v2/order/paymentcredit/creditaccounts/{repdid}` | Get credit accounts for a rep, optionally scoped to a customer or a specific credit account. |
| `GET` | `/api/v2/payments/checkoutpaymentoption/{onlineorderid}` | List the payment methods that can pay for a checkout order. |
| `GET` | `/api/v2/payments/checkoutpaymentoption/credits/{onlineorderid}` | List the account credits the shopper can apply to a checkout order. |
| `GET` | `/api/v2/payments/checkoutpaymentoption/giftcertificates/{onlineorderid}` | List the gift certificates the shopper can apply to a checkout order, flagging any that can't be used with the items in the cart. |
| `GET` | `/api/v2/payments/checkoutpaymentoption/validategiftcertificate/{giftcertificatedid}` | Check a gift certificate code and return its amount, the amount used and its remaining balance. |
| `POST` | `/api/v2/payments/checkoutpaymentoption/applygcsandcredits/{onlineorderid}` | Apply gift certificates or account credits to a checkout order and return the amount applied for each. |

### Shipping

| Method | Path | Description |
| --- | --- | --- |
| `POST` | `/api/v2/shipping/orderdetailtrackingquantity` | Record tracking numbers and shipped quantities against order line items, optionally sending shipping-notification emails. |
| `POST` | `/api/v2/shipping/trackingimportar/{warehouseid}` | Import tracking numbers for a batch of orders shipped from a warehouse, optionally sending shipping-notification emails. |

### Customers and reps

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/api/v2/customer/{id}` | Get profile information for a customer by customer DID. |
| `POST` | `/api/v2/customer` | Create a new customer for a rep. |
| `POST` | `/api/v2/customer/lookup` | Look up customers by DID, name, phone or email. |
| `GET` | `/api/v2/rep/{id}` | Get profile information for a rep by rep DID or replicated-site URL. |
| `PUT` | `/api/v2/rep/{id}` | Update a rep's details. Only the fields in the body are changed. |
| `GET` | `/api/v2/rep/publicinfo/{id}` | Get the public information for an active or cancelled rep. |
| `GET` | `/api/v2/rep/publicinfo/replookup/{email}` | Look up a rep's public information by email. |
| `GET` | `/api/v2/rep/downlinesummary/{repdid}` | Get a summary of a rep's downline. |
| `GET` | `/api/v2/rep/bestsellers/{repdid}` | Get the best-selling products for a rep. |
| `GET` | `/api/v2/rep/autoship/volumesummary/{repdid}` | Get the autoship volume summary for a rep. |
| `GET` | `/api/v2/rep/dashboard/summary/{id}` | Get a rep's current rank, next rank, progress toward it and projected commissions. |

### Parties

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/api/v2/party/{partyid}` | Get a specific party by ID. |
| `GET` | `/api/v2/party/revolution/openbyrep/{repdid}` | Get the open parties for a rep. |

### Payouts

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/api/v2/payoutmethod/list` | List the available payout methods. |
| `GET` | `/api/v2/payoutmethod/captions/{payoutmethodid}` | Get the field labels for a payout method, used to build payout profiles. |
| `POST` | `/api/v2/rep/payoutprofile` | Create a rep's payout profile for a payout method. |

### Authentication

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/api/v2/authentication/seamless/forrep/{repdid}` | Get a seamless sign-in token for a rep, for signing in to other Freedom Suite applications. |
| `GET` | `/api/v2/authentication/seamless/forcustomer/{customerdid}` | Get a seamless sign-in token for a customer, for signing in to other Freedom Suite applications. |
| `GET` | `/api/v2/authentication/seamless/verify/forrep/{guid}` | Exchange a one-time seamless sign-in token for the rep's identity and an access token. Each token works once. |
| `GET` | `/api/v2/authentication/seamless/verify/forcustomer/{guid}` | Exchange a one-time seamless sign-in token for the customer's identity and an access token. Each token works once. |

## Security checklist

You are responsible for keeping your client ID, client secret and API key secure. Anyone who holds them can call the API as your integration, read your data and make changes on your behalf. ByDesign treats every request made with your credentials as coming from you.

- **Store credentials in a secrets manager**, such as [AWS Secrets Manager](https://aws.amazon.com/secrets-manager/), [Azure Key Vault](https://azure.microsoft.com/products/key-vault), [Google Cloud Secret Manager](https://cloud.google.com/secret-manager) or [HashiCorp Vault](https://www.vaultproject.io), and load them at runtime. Never hard-code them, commit them to source control, or share them by email or chat.
- Limit who and what can read the secrets to the people and services that need them.
- Call the API only from server-side code over HTTPS.
- Send credentials only in headers or the token request body, never in URLs or query strings.
- Request only the scopes your integration needs.
- Rotate your client secret and API key in the ByDesign back office if you suspect they've been exposed.
