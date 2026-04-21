# R3 — Regolith Co. Public Data Surface

**Purpose:** document Regolith Co.'s public data model so Golem Mining Suite can build an importer that absorbs Regolith users when they shut down **June 1, 2026**.

**Primary source artifacts** (downloaded to `tasks/research/_regolith_schema/`):
- `sessions.gql`, `workorders.gql`, `scouting.gql`, `crewshares.gql`, `loadouts.gql`, `user.gql`, `lookups.gql`, `pagination.gql`, `events.gql`, `vehicle.gql`, `queries.gql`
- All pulled from `https://github.com/RegolithCo/RegolithCo-Common` (branch `main`, MIT-licensed, `@regolithco/common` on npm).

---

## 1. Shutdown Context

**Source:** https://docs.regolith.rocks/blog/2026/03/10/GoodBye-Regolith/
**Author:** Raychaser. Published **March 10, 2026**. 8-min read.

### Key verbatim quotes

> "I've made the difficult decision to shut down Regolith permanently. The site will not receive updates going forward but will stay online until June 1, 2026."

> "Regolith was initially designed around the way mining was when it first launched in 2022. … The breaking point is coming soon and at that point we're talking about gutting nearly everything under the hood and starting over. Here's the hard truth: **I just don't have the time to keep up with the pace of Star Citizen's development anymore.** I've decided to pull the plug before the tool becomes completely useless…"

> "I'm sorry to bow out before these tools come online but maybe others (looking at you SC-trade tools and UEX) can pick up a bit of the slack to fill the small void Regolith is leaving behind."

> "By a very rough count nearly 31,000 people have tried it and the daily usage metrics are pretty damned respectable."

### Timeline

| Date | Event |
|---|---|
| Immediate (Mar 10 2026) | Ko-Fi & Patreon donations turned off |
| **June 1, 2026** | `regolith.rocks` site shut down permanently |
| **June 1, 2026** | Discord server (~1,200 members) shut down |
| TBD | Merch store stays up indefinitely |

### Export tool — verbatim

> "The actual Regolith site will stay up for a couple of months in order to give folks a chance to **screenshot any data or for more savvy users to pull their data out of the API** if they want it."

**Explicit interpretation:** Raychaser is **NOT** shipping a purpose-built "export everything" button for the shutdown. The stock in-app per-session **CSV/JSON download** that has always existed (because DB wipes happen during every SC patch) is the only official export surface. The FAQ states:

> "be sure to regularly download your sessions to either a CSV or JSON files."

This matters: the per-session download button is the one guaranteed export path for non-technical users. Power users can also hit the GraphQL API directly with an API key they generate from their profile page. See §4 for format details.

### Refusal to sell / hand off — verbatim

> "The only real reason anyone would pay money for Regolith is to gain access to its users so that just always felt gross to me. As for handing it off I don't think that would work either."

Implication for us: there will be no "buy the userbase" pathway. We must win users on merit + a frictionless importer.

### Post-shutdown contact

> "If anyone wants to contact me beyond June 1, 2026, I'll keep the Raychaser bluesky account active." — `https://bsky.app/profile/raychaser.regolith.rocks`

---

## 2. Public API Surface

### 2.1 Base endpoint

| Property | Value |
|---|---|
| Production GraphQL endpoint | **`https://api.regolith.rocks`** (confirmed, docs + github) |
| Staging domain observed | `regolith.sinfulshadows.com` (personal domain of creator, not in public docs — likely internal/staging; treat as out-of-scope for importer) |
| Frontend | `https://regolith.rocks` |
| Docs | `https://docs.regolith.rocks` (Docusaurus) |
| Protocol | **GraphQL over HTTP POST** — single endpoint, not REST |
| Rate limit | **3,600 req/day** per API key (base tier). "If you need more, please contact us on Discord." |

### 2.2 Authentication model

Three auth types are declared in the schema (`lookups.gql` → `AuthTypeEnum`):

```graphql
enum AuthTypeEnum {
  GOOGLE      # Google OAuth (user login)
  DISCORD     # Discord OAuth (user login)
  API_KEY     # Personal API key (for pull scripts / our importer)
}
```

- **User login (browser)** → Discord OAuth OR Google OAuth. Both issue a session cookie / JWT the SPA uses on `Authorization` headers to `api.regolith.rocks`.
- **Programmatic access (our importer)** → user must go to their Regolith profile page, generate a personal API key (the `userAPIKey` mutation — see schema), and paste it into Golem's importer wizard. Header: `x-api-key: <token>`.
- Directive-level enforcement: every authenticated field carries `@logged_in`. Admin-only fields carry `@admin_only`. Public fields (e.g. `sessionShare`, `lookups`) have no directive.

### 2.3 Canonical request shape

```
POST https://api.regolith.rocks
Content-Type: application/json
x-api-key: <user_token>

{
  "query": "query { profile { userId scName avatarUrl createdAt updatedAt } }"
}
```

Sample response (structure; field meanings in §3.5):

```json
{
  "data": {
    "profile": {
      "userId": "a7f1…-uuid",
      "scName": "RaychaserSC",
      "avatarUrl": "https://…",
      "createdAt": 1701234567000,
      "updatedAt": 1712345678000
    }
  }
}
```

`Timestamp` is a custom scalar **encoded as a number (epoch ms)** per `codegen.yml`. `BigInt` is a custom scalar encoded as a string-safe bigint. `JSONObject` is a free-form `Record<string, any>`.

### 2.4 Every query and mutation (verbatim from `queries.gql`)

#### Queries (read-side — what the importer cares about)

| Field | Args | Return | Auth | Importer use |
|---|---|---|---|---|
| `profile` | — | `UserProfile` | `@logged_in` | **Root of the import tree.** Contains loadouts, sessionSettings, friends, defaults, and paginated `mySessions` / `joinedSessions` / `workOrders`. |
| `user(userId: ID!)` | user guid | `User` | `@logged_in` | Resolve display names for foreign `ownerId`/`payeeUserId` references. |
| `session(sessionId: ID!)` | session guid | `Session` | `@logged_in` | Fetch one session + all nested members, work orders, scouting finds. |
| `sessionShare(joinId: ID!)` | public join guid | `SessionShare` | PUBLIC | Sanitized read-only view for public URLs. Not needed for import. |
| `sessionUser(sessionId: ID!)` | — | `SessionUser` | `@logged_in` | My role in a given session. |
| `workOrder(sessionId: ID!, orderId: ID!)` | — | `WorkOrder` (union) | `@logged_in` | Fetch one work order of any type. |
| `scoutingFind(sessionId: ID!, scoutingFindId: ID!)` | — | `ScoutingFind` (union) | `@logged_in` | Fetch one scan cluster. |
| `crewShares(sessionId: ID!, orderId: ID, nextToken: String)` | — | `PaginatedCrewShares` | `@logged_in` | Payout rows (paginated). |
| `sessionUpdates(sessionId: ID!, lastCheck: String)` | — | `[SessionUpdate]` | `@logged_in` | Change-feed; we won't need this. |
| `submitOCRImage(sessionId, captureType, metadata)` | — | `String` (signed URL) | `@logged_in` | OCR ingestion path; irrelevant for import. |
| `lookups` | — | `LookupData` | PUBLIC | CIG/UEX ore densities, refinery bonuses, tradeports, ship specs. Useful reference data we might want to mirror. |
| `surveyData(epoch: String!, dataName: String!)` | — | `SurveyData` | PUBLIC | Aggregate Survey Corps ore-location stats. Bonus nice-to-have (see §8 community sentiment — Survey Corps is a top feature users love). |

#### Mutations (write-side — we will **not** call these, but they define shape)

Full list from `queries.gql` lines 128–499: `updateUserProfile`, `refreshAvatar`, `setUserPlan` (admin), `userAPIKey`, `deleteUserProfile`, `requestVerifyUserProfile`, `verifyUserProfile`, `addFriends`, `removeFriends`, `blockProspector`, `createWorkOrder`, `updateWorkOrder`, `claimWorkOrder`, `failWorkOrder`, `deliverWorkOrder`, `deleteWorkOrder`, `createSession`, `updateSession`, `updatePendingUsers`, `addSessionMentions`, `removeSessionMentions`, `removeSessionCrew`, `deleteSession`, `joinSession`, `upsertSessionUser`, `updateSessionUser`, `rotateShareId`, `leaveSession`, `markCrewSharePaid`, `upsertCrewShare`, `deleteCrewShare`, `addScoutingFind`, `updateScoutingFind`, `joinScoutingFind`, `leaveScoutingFind`, `deleteScoutingFind`, `createLoadout`, `updateLoadout`, `deleteLoadout`, `mergeAccount`, `setLookupData` (admin).

`captureRefineryOrder(imgUrl: String!)` → `ShipMiningOrderCapture` is additionally exposed on the **deployed** schema (seen in docs at `/api/operations/queries/capture-refinery-order`) but is not in the public common repo — it's a server-only OCR processing hook.

### 2.5 Subscriptions

The file `events.gql` defines `APIEvent` + `APIEventTypeEnum` (CREATED/UPDATED/DELETED/JOINED/LEFT + MJ_*/RC_*/CS_* variants), but no `type Subscription` block is declared in `queries.gql`. Subscriptions are live-session realtime — **not needed for a one-shot import** and we can ignore.

---

## 3. Data Models (reverse-engineered from schema)

All types live under the GraphQL schema in `@regolithco/common`. Scalar mapping per `codegen.yml`: `Timestamp = number (ms epoch)`, `BigInt = bigint (aUEC amounts — can exceed 2³²)`, `JSONObject = Record<string,any>`, `RockType = string` (value from `AsteroidTypeEnum ∪ DepositTypeEnum`).

### 3.1 Session (root aggregate)

```
type Session {
  sessionId: ID!                       # GUID — primary key
  joinId: ID!                          # public share token (rotatable)
  ownerId: ID!                         # creator's userId
  owner: User                          # hydrated from ownerId
  createdAt, updatedAt, finishedAt: Timestamp
  state: SessionStateEnum              # ACTIVE | CLOSED (closes after 72h idle)
  version: String                      # Star Citizen semver (e.g. "4.3.1")
  name: String                         # optional, else auto-named "Saturday Session"
  note: String
  sessionSettings: SessionSettings!    # goals/gates/defaults — see below
  mentionedUsers: [PendingUser!]!      # invite roster (scName-only until they join)
  activeMembers: PaginatedSessionUsers # crew in-session now
  scouting:      PaginatedScoutingFinds
  workOrders:    PaginatedWorkOrders
  summary:       SessionSummary        # cached totals (aUEC, SCU, unpaid shares)
}
```

**SessionSettings** fields (all optional, user picks subset):
`activity` (VEHICLE_MINING | SHIP_MINING | SALVAGE | OTHER), `specifyUsers`, `allowUnverifiedUsers`, `usersCanAddUsers`, `usersCanInviteUsers`, `lockToDiscordGuild` (DiscordGuild{id,name,iconUrl}), `gravityWell`, `location` (SURFACE | CAVE | SPACE | RING), `systemFilter` (STANTON | PYRO | NYX), `lockedFields`, `controlledSessionRole`, `controlledShipRole`, plus a nested `WorkOrderDefaults` for pre-filled order fields.

**SessionSummary** is a computed snapshot (aUEC, collectedSCU, yieldSCU, allPaid, lastJobDone, refineries used, active/total members, workOrdersByType, scoutingFindsByType, per-order rollups). We should recompute it on import rather than trust it verbatim.

### 3.2 Crew (SessionUser + PendingUser)

```
type SessionUser {
  sessionId: ID!
  ownerId: ID!                # the user's own userId (NOT the session owner)
  owner: User
  createdAt, updatedAt: Timestamp
  isPilot: Boolean!
  sessionRole: String         # MANAGER | SCOUT | MEDICAL | SECURITY | LOGISTICS | TRANSPORT (string free-form)
  shipRole:    String         # PILOT | COPILOT | ENGINEER | TURRET | LASER_OPERATOR | SECURITY | MEDIC | STEVEDORE
  captainId: ID               # if passenger, who's the pilot?
  shipName:  String           # optional — e.g. "Molissa"
  state: SessionUserStateEnum # TRAVELLING | SCOUTING | ON_SITE | AFK | REFINERY_RUN | UNKNOWN
  vehicleCode: String         # UEX code for the ship they brought
  loadout: MiningLoadout      # snapshot
}

type PendingUser {            # "mentioned" but not logged in yet
  scName: String!
  captainId: ID
  sessionRole: String
  shipRole: String
}
```

### 3.3 Work Order (union of 4 concrete types — the refinery-job unit)

All four implement `WorkOrderInterface`:

```
orderId, sessionId, createdAt, updatedAt, ownerId, version: required
seller{scName,UserId} / seller: User   # delegate seller (optional)
state: WorkOrderStateEnum              # UNKNOWN | REFINING_STARTED | REFINING_COMPLETE | DONE | FAILED
failReason: String
includeTransferFee: Boolean
orderType: ActivityEnum                # VEHICLE_MINING | SHIP_MINING | SALVAGE | OTHER
note: String
expenses: [WorkOrderExpense!]          # {name, amount:BigInt, ownerScName}
isSold: Boolean
shareAmount: BigInt                    # manual override of calc'd sale price
sellStore: String                      # UEX code of the sell tradeport
crewShares: [CrewShare!]               # payout splits
session: Session                       # back-ref
```

Concrete variants (only the *extra* fields beyond the interface):

| Type | Extra fields |
|---|---|
| `ShipMiningOrder` | `shareRefinedValue: Boolean`, `isRefined: Boolean`, `processStartTime: Timestamp`, `processDurationS: Int`, `processEndTime: Timestamp`, `refinery: RefineryEnum`, `method: RefineryMethodEnum`, **`shipOres: [RefineryRow!]!`** = `[{ore: ShipOreEnum, amt: Int, yield: Int}]` |
| `VehicleMiningOrder` | **`vehicleOres: [VehicleMiningRow!]!`** = `[{ore: VehicleOreEnum, amt: Int}]` |
| `SalvageOrder` | **`salvageOres: [SalvageRow!]!`** = `[{ore: SalvageOreEnum, amt: Int}]` |
| `OtherOrder` | — (just the interface fields; for non-mining work) |

**Ore enums** (complete lists; watch for patch drift):
- `ShipOreEnum` (26 values): GOLD, TITANIUM, QUANTANIUM, QUARTZ, AGRICIUM, BERYL, BEXALITE, CORUNDUM, DIAMOND, HEPHAESTANITE, LARANITE, TARANITE, ALUMINUM, BORASE, COPPER, TUNGSTEN, IRON, ICE, SILICON, STILERON, TIN, RICCITE, TORITE, LINDINIUM, SAVRILIUM, INERTMATERIAL
- `VehicleOreEnum` (10 values): HADANITE, JANALITE, APHORITE, DOLIVINE, BERADOM, GLACOSITE, FEYNMALINE, JACLIUM, SALDYNIUM, CARINITE
- `SalvageOreEnum` (2 values): RMC, CMAT
- `RefineryEnum` (16 refineries across Stanton/Pyro/Nyx)
- `RefineryMethodEnum` (9 methods): CORMACK, ELECTROSTAROLYSIS, FERRON_EXCHANGE, DINYX_SOLVENTATION, GASKIN_PROCESS, KAZEN_WINNOWING, PYROMETRIC_CHROMALYSIS, THERMONATIC_DEPOSITION, XCR_REACTION

### 3.4 Scouting Find (union of 3 concrete types — rock scans)

All implement `ScoutingFindInterface` with `sessionId`, `scoutingFindId`, timestamps, `clusterType`, `version`, `clusterCount`, `ownerId`, `note`, `state` (DISCOVERED | READY_FOR_WORKERS | WORKING | DEPLETED | ABANDONNED [sic]), `attendanceIds`, `gravityWell` (UEX code), `includeInSurvey`, `surveyBonus`, `score`, `rawScore`.

| Type | Payload |
|---|---|
| `ShipClusterFind` | `shipRocks: [ShipRock!]!` = `[{state:RockStateEnum, mass:Float, inst:Float, res:Float, rockType:RockType, ores:[{ore:ShipOreEnum, percent:Float}]}]` |
| `VehicleClusterFind` | `vehicleRocks: [VehicleRock!]!` = `[{mass, inst, res, ores:[{ore:VehicleOreEnum, percent:Float}]}]` |
| `SalvageFind` | `wrecks: [SalvageWreck!]!` = `[{state:WreckStateEnum, isShip:Boolean, shipCode:String, sellableAUEC:BigInt, salvageOres:[{ore:SalvageOreEnum, scu:Int}]}]` |

`RockType` is a hybrid scalar — value is a string drawn from **either** `AsteroidTypeEnum` (CTYPE, ETYPE, MTYPE, PTYPE, QTYPE, STYPE, ITYPE-Pyro) **or** `DepositTypeEnum` (ATACAMITE, FELSIC, GNEISS, GRANITE, IGNEOUS, OBSIDIAN, QUARTZITE, SHALE). Importer must treat it as a string and validate against the union.

### 3.5 User / UserProfile

```
type User                         # lightweight, visible to everyone
  userId, scName, avatarUrl, createdAt, updatedAt, state(UNVERIFIED|VERIFIED)

type UserProfile (extends User)   # own-profile only, requires @logged_in
  lastActive: Timestamp
  verifyCode: String
  plan: UserPlanEnum              # FREE | ETERNAL_GRATITUDE | GRIZZLED_PROSPECTOR | ADMIN
  apiKey: String                  # the user's own x-api-key token
  friends: [String!]              # scNames only
  userSettings: JSONObject        # arbitrary client prefs
  discordGuilds: [MyDiscordGuild] # Discord server list w/ hasPermission bool
  sessionSettings: SessionSettings
  sessionShipCode: String         # default ship for new sessions
  deliveryShipCode: String        # default ship for delivery
  loadouts: [MiningLoadout!]
  workOrders:    PaginatedWorkOrders
  mySessions:    PaginatedSessions
  joinedSessions:PaginatedSessions
  # Survey Corps
  surveyorName:    String
  surveyorGuild:   DiscordGuild
  isSurveyor:      Boolean
  isSurveyorBanned:Boolean
  surveyorScore:   Int
```

### 3.6 Payout / Crew Share

```
type CrewShare {
  sessionId, orderId: ID!
  createdAt, updatedAt: Timestamp
  payeeScName: String!       # ALWAYS present — survives user deletion/rename
  payeeUserId: ID            # nullable — only for registered payees
  shareType: ShareTypeEnum   # PERCENT | AMOUNT | SHARE
  share: Float               # 0–1 for PERCENT, aUEC for AMOUNT, integer for SHARE
  note: String
  state: Boolean             # true=paid, false/null=unpaid
  workOrder: WorkOrder       # back-ref
  session: Session
}

type CrewShareTemplate        # default splits on session/user profile
  payeeScName, shareType, share, note
```

### 3.7 Ship Loadout

```
type MiningLoadout {
  loadoutId: ID!
  owner: User!
  name: String!
  createdAt, updatedAt: Timestamp
  ship: LoadoutShipEnum              # PROSPECTOR | MOLE | GOLEM | ROC
  activeLasers: [ActiveMiningLaserLoadout]!
  inventoryLasers:  [MiningLaserEnum!]!
  inventoryModules: [MiningModuleEnum!]!
  inventoryGadgets: [MiningGadgetEnum!]!
  activeGadgetIndex: Int
}

type ActiveMiningLaserLoadout {
  laser: MiningLaserEnum!            # LancetMH1/MH2, HofstedeS0/1/2, KleinS0/1/2,
  laserActive: Boolean!              # ImpactI/II, Helix0/I/II, ArborMH1/MH2/MHV,
  modules: [MiningModuleEnum]!       # Pitman, Lawson
  modulesActive: [Boolean!]!
}
```

Module enum has 26 values (Brandt, Forel, Lifeline, Optimum, Rime, Stampede, Surge, Torpid, FLTR*, Focus*, Rieger*, Torrent*, Vaux*, XTR*). Gadget enum has 6 (Optimax, Okunis, Sabir, Stalwart, Boremax, Waveshift).

---

## 4. Export Formats Regolith Offers Users

**Official position (FAQ verbatim):**

> "be sure to regularly download your sessions to either a CSV or JSON files."

### What exists today (pre-shutdown)

1. **Per-session CSV download** — button on each session page. Flattens the work-order table and crew-share rows into rows that mirror what their "mining spreadsheet" ancestors did. Good for humans in Excel, lossy for nested structure (loadouts, rock scans, rich summaries).
2. **Per-session JSON download** — button on each session page. Dumps the session payload as the API returns it (GraphQL-shaped), preserving the union types and nested arrays. **This is the format our importer should primarily target** because it's lossless.
3. **GraphQL API pull** — power users can generate a personal `x-api-key` via the `userAPIKey` mutation, then page through `profile → mySessions / joinedSessions / workOrders` themselves. **The importer's "advanced" path should offer to do this automatically** given an API key.

### What does NOT exist

- No "download everything for all my sessions" single-click bulk export button confirmed in the UI as of the shutdown blog post (2026-03-10).
- No mass DB dump published by Raychaser. The GitHub repos contain schema + OCR code only — no user data.
- No Discord-server transcript export.
- No Survey Corps aggregate dataset dump (live queryable via `surveyData` query until shutdown, then gone).

### Community-confirmed gap

Lemmy user **macniel@feddit.org** (`https://lemmy.world/post/44559558`) observes the repo "only contains the server code, and its not documented, and no database records" — confirming that the self-host path is not viable.

### Recommendation

Our importer should accept **two input paths**:

1. **"I downloaded my sessions"** — drag-and-drop one or many `*.json` (and optionally `*.csv`) files. Schema-validate against the types in §3, surface a diff/preview before committing.
2. **"Connect my Regolith API key"** — user pastes their x-api-key, we run a paged GraphQL pull (`profile { mySessions, joinedSessions, workOrders, loadouts, ... }`), with retry/backoff respecting the 3,600/day cap. This path is strictly better (richer data, zero manual steps) and must be live **well before June 1, 2026**.

---

## 5. Authentication Details

| Aspect | Value |
|---|---|
| User login | Discord OAuth **or** Google OAuth |
| OAuth scopes (Discord) | Not documented publicly. Given the schema surfaces `discordGuilds` and a `lockToDiscordGuild` gate, they at minimum request **`identify` + `guilds`** (standard read-your-servers scope). Email scope likely also requested. _Confirmation gap — ask Raychaser on Bluesky if precise scope list matters._ |
| OAuth scopes (Google) | Not documented. Almost certainly `openid profile email` only (used just to identify the user). |
| Verified-user flow | Optional. User adds a `verifyCode` to their RSI bio; Regolith scrapes CIG's page (`verifyUserProfile` mutation) and flips `state: VERIFIED`. Required to participate in Survey Corps. |
| Session identification | Opaque `Authorization: Bearer …` JWT/cookie set by OAuth redirect — irrelevant to us because we don't do browser SSO, we take API keys. |
| **Programmatic auth (the path the importer uses)** | Single header `x-api-key: <token>`. Token is generated per-user via the `userAPIKey` mutation and visible/revocable on the profile page. |
| Rate limit | 3,600 req/day base. GraphQL, so we can batch heavily — one `session(sessionId){ all fields + nested paginated children }` query gets you most of a session in 1 request. |
| Account deletion | `deleteUserProfile(leaveData: Boolean)` — importer ingest must preserve `payeeScName` on crew shares because `payeeUserId` may become dangling after a payee deletes their Regolith account. |

---

## 6. Their Tech Stack (inferred)

### Confirmed

- **Docs site:** Docusaurus v3.9.2 (per `<meta name="generator">` on the goodbye blog), hosted via GitHub Pages (the `Regolith-Docs` repo has `has_pages: true`).
- **Frontend SPA:** React + Apollo Client (`@apollo/client` is the runtime dep in `@regolithco/common`'s `package.json`). SPA build uses Vite/ESM (the common repo ships dual CJS+ESNext builds for "Vite / browser"). Framework is React (matches the creator's comment: "grown from a simple little React app into a surprisingly complicated tech stack").
- **Shared data layer:** `@regolithco/common` — TypeScript package with GraphQL schema + codegen types (`graphql-codegen` → `schema.types.ts`), shared utilities. MIT licensed.
- **OCR engine:** **Browser-based PaddleOCR via `@gutenye/ocr-browser` + `onnxruntime-node`** (the `RegolithCo-OCR` repo). Runs client-side in the browser; the `submitOCRImage` query returns a signed upload URL so the server can process/store large captures. Notably NOT Tesseract.
- **GraphQL server:** bespoke server at `api.regolith.rocks`. Uses custom directives (`@logged_in`, `@admin_only`, `@example`) and custom scalars (`Timestamp`, `BigInt`, `JSONObject`, `RockType`).

### Strongly inferred (not directly confirmed — gap)

- **Hosting / backend:** almost certainly AWS. Strong signals:
  - `sessionShare` type references "the Lambda SSR to render the page" verbatim in a comment in `sessions.gql` line 110: `# The public URL shares the session id and a few other things needed for # The Lambda SSR to render the page`. → **AWS Lambda confirmed.**
  - `x-api-key` header convention is AWS API Gateway / AppSync idiom.
  - GraphQL + `x-api-key` + Lambda + single-endpoint = almost certainly **AWS AppSync** backed by **DynamoDB** (explains the pagination pattern with `nextToken: String` strings, which are AppSync/DynamoDB idiom).
  - OAuth likely flows through **AWS Cognito** federated identity providers (Discord + Google).
- **Domain:** Route53 + CloudFront edge for static assets; Cognito hosted UI for OAuth.
- **Image store:** S3 for OCR uploads (signed URLs from `submitOCRImage`).
- **Third-party data:** **UEX** (uexcorp.space) for ship / tradeport / gravity-well lookups — `UEXLookups` type surfaces `bodies`, `maxPrices`, `ships`, `tradeports`, `refineryBonuses`. **CIG** for base ore densities, method bonuses, ore processing table.

The only public repos in the `RegolithCo` GitHub org are `RegolithCo-Common`, `Regolith-Docs`, and `RegolithCo-OCR`. **The frontend app and the backend Lambda code are closed-source.**

---

## 7. What an Importer Needs (C# mapping)

Golem is a .NET project. Below is a draft C# type contract for deserializing a Regolith session JSON export. Use `System.Text.Json` (or Newtonsoft if nested union polymorphism gets painful).

### 7.1 Scalar strategy

| Regolith scalar | C# type | Notes |
|---|---|---|
| `ID` | `string` (Guid as string) | Always string in JSON — do not `Guid.Parse` eagerly; some IDs are AWS KSUID-ish not strict GUIDs. |
| `Timestamp` | `long` (epoch ms) → convert to `DateTimeOffset` | `DateTimeOffset.FromUnixTimeMilliseconds(n)` |
| `BigInt` | `long` or `decimal` — **watch overflow** | aUEC can exceed `int.MaxValue`. Use `long` minimum; `decimal` safest for display. |
| `JSONObject` | `JsonElement` or `Dictionary<string, object?>` | Keep lazy. |
| `RockType` | `string` | Value is from AsteroidTypeEnum ∪ DepositTypeEnum. |
| Enums | C# `enum` with `[JsonStringEnumConverter]` | Must stay lenient — add `Unknown = 0` fallback for future SC-patch values. |

### 7.2 Union polymorphism

GraphQL exposes three unions we must handle: `WorkOrder = SalvageOrder | ShipMiningOrder | VehicleMiningOrder | OtherOrder`, `ScoutingFind = ShipClusterFind | VehicleClusterFind | SalvageFind`, and `SessionUpdateUnion` (ignore — not in export). In the JSON export each member includes a `__typename` field — use a custom `JsonConverter<RegolithWorkOrder>` that reads `__typename` and dispatches to the concrete subtype. Alternatively, collapse to a single flat DTO with a discriminator `OrderType` + nullable ship/vehicle/salvage ore arrays.

### 7.3 Field mapping table (Regolith → Golem)

| Regolith | Golem equivalent (proposed) | Notes |
|---|---|---|
| `Session.sessionId` | `MiningSession.ExternalId` (+ keep origin="regolith") | Preserve for idempotent re-import. |
| `Session.name / note / version` | `MiningSession.Name / Notes / GameVersion` | 1:1. |
| `Session.state` | `MiningSession.Status` (Active/Closed) | Enum map. |
| `Session.createdAt / finishedAt` | `MiningSession.StartedUtc / EndedUtc` | epoch-ms → DateTimeOffset. |
| `Session.ownerId` / `owner.scName` | `MiningSession.OwnerScName` | We map by scName, not foreign userId. |
| `Session.sessionSettings.activity` | `MiningSession.PrimaryActivity` | Map VEHICLE_MINING/SHIP_MINING/SALVAGE/OTHER. |
| `Session.sessionSettings.location / systemFilter / gravityWell` | `MiningSession.LocationTag` (compose) | Flatten to a single geo tag string or three columns. |
| `SessionUser.ownerId / owner.scName` | `Crew.ScName` | Use scName as the stable key. |
| `SessionUser.sessionRole / shipRole` | `Crew.SessionRole / Crew.ShipRole` | Accept arbitrary string + parse known enums. |
| `SessionUser.shipName / vehicleCode` | `Crew.ShipName / Crew.ShipUexCode` | 1:1. |
| `SessionUser.state` | `Crew.LiveStatus` | Enum map (TRAVELLING/SCOUTING/ON_SITE/AFK/REFINERY_RUN/UNKNOWN). |
| `PendingUser` | `Crew` with `Pending=true` | Import as placeholder crew rows. |
| `WorkOrderInterface.orderId` | `RefineryJob.ExternalId` | Preserve. |
| `WorkOrderInterface.orderType` | `RefineryJob.OrderType` | Enum. |
| `WorkOrderInterface.state / failReason` | `RefineryJob.Status / FailReason` | Map REFINING_STARTED/COMPLETE/DONE/FAILED/UNKNOWN. |
| `WorkOrderInterface.isSold / shareAmount / sellStore` | `RefineryJob.IsSold / SaleAmountAuec / SellStoreUexCode` | BigInt → long. |
| `WorkOrderInterface.includeTransferFee` | `RefineryJob.IncludeTransferFee` | bool. |
| `WorkOrderInterface.expenses[]` | `RefineryJob.Expenses[]` | `{Name, AmountAuec, OwnerScName}`. |
| `ShipMiningOrder.refinery / method / processStartTime / processDurationS / shareRefinedValue / isRefined` | Same-name fields on `RefineryJob` | Process end time is derivable; prefer start + duration. |
| `ShipMiningOrder.shipOres[]` | `RefineryJob.ShipOreRows[]` = `{Ore, Amount, Yield}` | ShipOreEnum. |
| `VehicleMiningOrder.vehicleOres[]` | `RefineryJob.VehicleOreRows[]` = `{Ore, Amount}` | VehicleOreEnum. |
| `SalvageOrder.salvageOres[]` | `RefineryJob.SalvageRows[]` = `{Ore, Amount}` | SalvageOreEnum. |
| `CrewShare.payeeScName` (PK) | `Payout.PayeeScName` | **Always present.** |
| `CrewShare.payeeUserId` | `Payout.PayeeExternalUserId` | Nullable. |
| `CrewShare.shareType / share` | `Payout.SplitType / SplitValue` | PERCENT/AMOUNT/SHARE with `share` float. |
| `CrewShare.state` | `Payout.IsPaid` | Boolean (null = unpaid). |
| `CrewShare.note` | `Payout.Note` | string. |
| `ScoutingFind.scoutingFindId / state / gravityWell / clusterCount / note` | `ScoutingFind.*` | 1:1. |
| `ShipClusterFind.shipRocks[]` | `ScoutingFind.ShipRocks[]` = `{State, MassKg, InstabilityPct, ResistancePct, RockType, OreComposition[]}` | `inst/res` are floats 0-1 (instability, resistance); `mass` is float in tonnes. |
| `ShipRockOre{ore, percent}` | `OreComposition{Ore, PercentOfRock}` | Percent 0-1. |
| `VehicleClusterFind.vehicleRocks[]` / `SalvageFind.wrecks[]` | Analogous | See §3.4. |
| `MiningLoadout.*` | `ShipLoadout.*` | Full object model maps 1:1. |
| `UserProfile.friends[]` | `User.FriendScNames[]` | Array of scNames. |
| `UserProfile.surveyorScore / isSurveyor / surveyorGuild` | `User.SurveyCorps{Score, OptIn, DiscordGuild}` | Preserve if we add a Survey-Corps analogue. |

### 7.4 Import order / dependencies

1. `UserProfile` (self) — gives us userId ↔ scName mapping for every foreign key.
2. Pull referenced users (`user(userId)`) to hydrate `ownerId` / `seller.userId` / `payeeUserId` into scNames. Cache locally.
3. Loadouts (no foreign deps).
4. Sessions (`mySessions` + `joinedSessions`), each with nested `workOrders`, `scouting`, `activeMembers`, `mentionedUsers`, and paginated `crewShares` per work order.
5. Deduplicate by `ExternalId` so re-imports are idempotent.

### 7.5 Edge cases the importer must handle

- Ore enums drift between SC patches. Always accept the string and fall back to `Unknown` / stash raw value in a sidecar column.
- `RockType` is a hybrid scalar — validate but don't enum-reject.
- `BigInt` values in JSON may arrive as strings or numbers depending on the GraphQL server's BigInt encoding. Accept both.
- `version` strings like `"4.3.1"` / `"4.3.1-ptu"` — keep raw, don't parse.
- `ABANDONNED` (sic) — misspelled enum value in the source schema. Map verbatim.
- `includeInSurvey: true` scouting finds may be the ONLY cut of this data available post-shutdown — capture them even if users don't immediately see value.
- Sessions where the user was a joiner (not owner) — they may not have full `ownerId` visibility; the `joinId` rotates; preserve the data as-observed.

---

## 8. Community Sentiment

Mining-community forums have been subdued; gathering comments was harder than expected. The Spectrum and Community Hub threads require JS rendering and weren't directly scrapeable; Reddit/SC-subreddit coverage is thin because the shutdown announcement is recent. Key source used successfully: Lemmy discussion plus references in search snippets.

### Quoted reactions

1. **macniel@feddit.org** (lemmy.world/post/44559558): _"I started to rely on this nifty Website especially for the mining jobs in this patch cycle. The UI was also beautifully crafted so its sad to see it go."_ Adds: the GitHub source "only contains the server code, and its not documented, and no database records" — blocking any community self-host effort.

2. **Raychaser** (author, in the shutdown blog comments-in-prose): _"losing the Discord makes me almost as sad as losing the site itself. I started the server as a cheap, easy way to collect bug reports and now there are 1,200 genuinely awesome people in there"_ — a strong signal that **community-centric features (Discord guild gating, session sharing, Survey Corps leaderboards)** are what bonded users, not just the calculator.

3. **Raychaser** on what can't be replaced: _"It's some of my best UI/UX design work and I'm insanely proud of all of it from the Survey Corps all the way to the custom OCR capture engine"_ — three pillars identified as core value: **(a) UI/UX polish, (b) Survey Corps crowd-sourced scan leaderboards, (c) OCR capture from screenshots.**

4. **Lemmy / Facebook search snippets** point to users asking "Alternative to regolith.rocks for star citizen data" (https://www.facebook.com/groups/713088543885937/posts/1436400631554721/) — the dominant ask is a **replacement**, not a self-host.

5. Raychaser explicitly names two successor candidates: **SC-trade.tools** and **UEX** (uexcorp.space). Neither currently covers sessions / crew-share / refinery-job tracking, which is Regolith's unique turf — this is our opportunity.

### What features users most want preserved (triangulated)

| Feature | Priority for importer / parity |
|---|---|
| Multi-user live sessions with role-based crew | **High** — table stakes |
| Refinery job tracker with 16-refinery, 9-method matrix + UEX price lookups | **High** |
| Crew share splits (percent / amount / share-count) with paid/unpaid toggle | **High** |
| Rock / cluster scan capture with ore composition | **High** |
| **OCR capture from SC screenshots** (refinery screen + rock-scan screen) | **High** — named as a beloved feature |
| **Survey Corps opt-in leaderboard** for crowd-sourced ore locations | **High** — named as a beloved feature; unique social hook |
| Discord-guild-gated session privacy | **Medium** |
| Mining loadout library (laser + module + gadget) | **Medium** — tied to upcoming 4.7 changes so may need rework anyway |
| Session public share URL | **Medium** |
| Friends list / user directory | **Low** |

---

## Gaps / open questions (would need to email Raychaser directly)

1. **Exact Discord OAuth scope list** (assumed `identify guilds email` but not confirmed in any public doc).
2. **Confirmation of AWS AppSync + DynamoDB + Cognito** — strongly inferred but not stated publicly. If we ever want to reverse-engineer a schema migration script directly from a DynamoDB dump, we'd need an off-the-record conversation.
3. **Bulk "export everything" button** — does one quietly exist in the profile page that isn't in the docs? If not, we should prompt users to generate an API key, which we already plan.
4. **Will the Survey Corps aggregated dataset be released as a static JSON dump before June 1?** This is crowd-sourced ore-location data with real value; worth asking Raychaser to publish a final parquet/JSON snapshot.
5. **Will Raychaser publish a migration-helpers blog post?** If he's willing to link to our importer from the docs site after shutdown, that's a huge user-acquisition win.

**Recommended contact:** Raychaser on Bluesky at `@raychaser.regolith.rocks` — he states it will stay active post-shutdown. Opening ask: permission to link Golem as the recommended migration target, and a nudge on the Survey Corps dataset dump.

---

## Appendix A — Sample end-to-end query for importer

One request to grab a whole session (use with `x-api-key`):

```graphql
query PullSession($sid: ID!) {
  session(sessionId: $sid) {
    sessionId joinId ownerId owner { userId scName }
    createdAt updatedAt finishedAt state version name note
    sessionSettings {
      activity specifyUsers allowUnverifiedUsers
      gravityWell location systemFilter
      lockToDiscordGuild { id name iconUrl }
      workOrderDefaults { includeTransferFee isRefined refinery method shareRefinedValue
                          sellStores { oreRefined oreRaw gem salvage }
                          crewShares { payeeScName shareType share note }
                          shipOres vehicleOres salvageOres }
    }
    mentionedUsers { scName captainId sessionRole shipRole }
    activeMembers { items {
      sessionId ownerId owner { userId scName }
      isPilot sessionRole shipRole captainId shipName state vehicleCode
      loadout { loadoutId name ship activeLasers { laser laserActive modules modulesActive }
                inventoryLasers inventoryModules inventoryGadgets activeGadgetIndex }
    } nextToken }
    scouting { items {
      __typename ... on ShipClusterFind {
        scoutingFindId createdAt updatedAt clusterCount ownerId note state
        gravityWell includeInSurvey surveyBonus score rawScore
        attendanceIds
        shipRocks { state mass inst res rockType ores { ore percent } }
      }
      ... on VehicleClusterFind { scoutingFindId createdAt updatedAt clusterCount ownerId note state
                                  gravityWell includeInSurvey surveyBonus score rawScore
                                  attendanceIds
                                  vehicleRocks { mass inst res ores { ore percent } } }
      ... on SalvageFind { scoutingFindId createdAt updatedAt clusterCount ownerId note state
                           gravityWell includeInSurvey surveyBonus score rawScore
                           attendanceIds
                           wrecks { state isShip shipCode sellableAUEC
                                    salvageOres { ore scu } } }
    } nextToken }
    workOrders { items {
      __typename ... on ShipMiningOrder {
        orderId createdAt updatedAt ownerId version
        sellerscName sellerUserId state failReason includeTransferFee
        orderType note isSold shareAmount sellStore
        expenses { name amount ownerScName }
        shareRefinedValue isRefined processStartTime processDurationS processEndTime
        refinery method shipOres { ore amt yield }
        crewShares { payeeScName payeeUserId shareType share state note createdAt updatedAt }
      }
      ... on VehicleMiningOrder { orderId createdAt updatedAt ownerId version
        sellerscName sellerUserId state failReason orderType note isSold shareAmount sellStore
        expenses { name amount ownerScName }
        vehicleOres { ore amt }
        crewShares { payeeScName payeeUserId shareType share state note createdAt updatedAt } }
      ... on SalvageOrder { orderId createdAt updatedAt ownerId version
        sellerscName sellerUserId state failReason orderType note isSold shareAmount sellStore
        expenses { name amount ownerScName }
        salvageOres { ore amt }
        crewShares { payeeScName payeeUserId shareType share state note createdAt updatedAt } }
      ... on OtherOrder { orderId createdAt updatedAt ownerId version
        sellerscName sellerUserId state failReason orderType note isSold shareAmount sellStore
        expenses { name amount ownerScName }
        crewShares { payeeScName payeeUserId shareType share state note createdAt updatedAt } }
    } nextToken }
  }
}
```

Plus the bootstrap query:

```graphql
query Me {
  profile {
    userId scName avatarUrl createdAt updatedAt lastActive plan apiKey friends
    surveyorName surveyorScore isSurveyor
    surveyorGuild { id name iconUrl }
    loadouts { loadoutId name ship
               activeLasers { laser laserActive modules modulesActive }
               inventoryLasers inventoryModules inventoryGadgets activeGadgetIndex }
    mySessions     { items { sessionId name state createdAt updatedAt finishedAt } nextToken }
    joinedSessions { items { sessionId name state createdAt updatedAt finishedAt } nextToken }
  }
}
```

Paginate with `nextToken` until null. Keep total calls comfortably under 3,600/day.
