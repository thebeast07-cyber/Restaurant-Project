# DESIGN.md — Restaurant POS UI

Direction supplied by the project owner (via chat, 2026-09-29), transcribed here for
`antislop.md` to apply during UI work. This is design *direction*, not a locked
spec — it can be revised as the UI takes shape and real feedback comes in.

## Identity

A modern, casual cafe — not a fine-dining establishment, not a traditional warung.
Concept direction: a **Chinese–Indonesian fusion** cafe. The concept is still young;
the exact menu/branding identity is not finalized, so this UI should stay flexible
rather than lock in literal motifs (no pastiche "Chinese takeout" or "batik pattern"
visual clichés — those would read as decoration for its own sake, not as an actual
expression of this specific business).

## Personality

**Modern, clean, efficient.** This was stated directly and is the dominant driver
for the UI's feel — it's a staff-facing operational tool (POS, not a customer-facing
menu/marketing site), so "efficient" should be read literally: fast to scan, low
visual noise, nothing that slows a cashier down mid-rush.

## Palette

**Not yet fixed.** No existing brand colors or logo to anchor to. Per antislop's
own rule (never invent content the owner hasn't supplied), no specific palette is
locked in this document — implementation should use a restrained, functional
palette (see `antislop-ui` guidance on color use) until the owner has real brand
colors to lock in. Revisit this section once that exists.

## Typography

Delegated to the agent by the owner ("terserah kamu tentukan"). Decision: a single
clean, modern sans-serif family across the UI (e.g. Inter or the system UI font
stack), rather than pairing in a second display face — this matches "modern, clean,
efficient" directly, and avoids reaching for a stereotypically "Asian-style" display
font to signal the fusion concept, which would be a visual cliché standing in for
actual identity work rather than an honest expression of this specific cafe.

## Mood

Efficient and unfussy over decorative. This is a tool operated under time pressure
(orders, checkout, shift handoffs) — clarity and speed of scanning beat visual
flourish. Revisit toward more "fusion" personality in customer-facing surfaces
later (e.g. if a self-order/menu-display screen is ever built) — the POS/back-office
screens this roadmap covers are staff tools first.

## Dial

Given the above (efficiency-first, no fixed palette yet, no strong energetic
brief): `ENERGY 2 / RHYTHM 2 / MOTION 1` — calm and clear, not sterile, not showy.
