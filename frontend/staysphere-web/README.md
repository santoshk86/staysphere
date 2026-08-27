# StaySphere Web

Next.js 16 (App Router) guest booking client for the StaySphere API.
Stack: TypeScript, Tailwind CSS v4, React 19.

## Setup

```bash
npm install
cp .env.example .env.local   # adjust NEXT_PUBLIC_API_BASE_URL if needed
```

`NEXT_PUBLIC_API_BASE_URL` defaults to `http://localhost:5276` (the API's dev
URL). The browser calls the API directly, and the API's CORS policy allows
`http://localhost:3000`, so run the dev server on port 3000.

## Scripts

| Command | Description |
|---------|-------------|
| `npm run dev` | Dev server (Turbopack) on http://localhost:3000 |
| `npm run build` | Production build |
| `npm start` | Serve the production build |
| `npm run lint` | ESLint |
| `npx tsc --noEmit` | Type-check |

Run the [StaySphere API](../../Backend) first, then `npm run dev`.

## Structure

```
src/
├── app/                     routes (see Docs/progress.md for the route table)
│   ├── page.tsx             /                       landing + search
│   ├── rooms/               /rooms, /rooms/[roomId] search results + details
│   └── booking/             /booking/[roomId], /booking/confirmation/[reference]
├── components/              presentational + two Client Components
│   ├── SearchForm.tsx       (client) search inputs → /rooms?…
│   └── BookingForm.tsx      (client) guest details → POST /api/reservations
└── lib/
    ├── api.ts               centralised API client + ApiError
    ├── config.ts            API base URL (env)
    ├── types.ts             DTOs mirroring Docs/api.md
    ├── validation.ts        shape-only input validation (backend is authoritative)
    └── format.ts            currency / date / nights helpers
```
