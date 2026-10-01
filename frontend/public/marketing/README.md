# Landing assets

The Aurora PNGs are unchanged product screenshots supplied for the landing.
Presentation crops are applied in CSS; no product content is fabricated.

## Approved brand assets

The approved vector assets live in `frontend/public/comvy-*.svg`. The public
header uses `comvy-logo-dark.svg` on navy. It keeps its link, accessible name,
sticky position, and layout. Other shared product brands use the lockup that
matches the current theme; compact headers use `comvy-symbol.svg`.

## Commercial destination pending

No verified commercial email, telephone or contact URL was found in the
repository. `Contato` and both `Quero conhecer` links lead to `#contato`,
which truthfully reports that the channel is not yet available on this page.
Provide a real destination before replacing this state with a contact link.
There is no lead form, submission, backend integration or invented contact.

## Lot 1.2 scrolling and verification

Desktop (1024px and above) uses native CSS `y proximity` on the hero,
communication chaos, WhatsApp thesis and transformation. An end alignment on
the contact boundary keeps the final content reachable. Explicit hash navigation
disables snapping so destinations take priority. Initial hashes are restored once
after React mounts; no wheel interception or continuous scroll listener is used.
Mobile uses ordinary scrolling. Reduced motion removes snapping, smooth scrolling,
animations and transitions.

Validated in the local browser at 1440px, 1024px and 390px: layout, real image
crops, header destinations, direct hash loading, commercial fallback and login.
Desktop short/long wheel gestures, Page Up/Down and Ctrl+Home/End worked;
short gestures could stop between scenes. Tab focus and Enter activation worked.
The login route retained ordinary scrolling without landing styles.

Physical trackpad and browser reduced-motion emulation were unavailable in the
automation surface. Reduced-motion behavior was checked through the focused test
and CSS rules. Scrollbar dragging could not be verified reliably with the browser
automation; manual confirmation remains necessary.

Validation: 20 focused tests passed, changed-file ESLint passed, production build
passed, and whitespace checks passed. Existing React Router future-flag, SignalR
annotation and shared UI bundle-size warnings remain. No backend or migrations
were changed; no commit or push was performed.
