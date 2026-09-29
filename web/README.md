# Restaurant POS — Web UI

React + TypeScript + Vite frontend for the Restaurant ERP backend
(`../src/Restaurant.Api`). See
[`docs/architecture/02-ui-roadmap.md`](../docs/architecture/02-ui-roadmap.md) for the
phase plan, and [`DESIGN.md`](DESIGN.md) for the design direction this UI follows.

## Running locally

Requires the backend running first (see the root `README.md`), then:

```bash
npm install
npm run dev
```

Opens on `http://localhost:5173`, calling the API at the URL in `.env`
(`VITE_API_BASE_URL`, defaults to `http://localhost:5291`).

## Design guidance

This project follows [`antislop.md`](antislop.md) (a filter against generic
AI-shaped UI/copy/code) alongside `DESIGN.md` (the actual style direction) for all
UI work — see both files before adding a new screen.
