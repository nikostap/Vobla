# 14. Начальный backlog

Приоритеты:

- `P0` — необходимо для первого рабочего сценария.
- `P1` — необходимо для полноценного MVP.
- `P2` — развитие.
- `Future` — закладывается архитектурно.

## Epic A. Repository и developer experience

- P0: создать solution и модульные проекты.
- P0: добавить Docker Compose.
- P0: добавить конфигурацию Development/Staging.
- P0: health checks.
- P0: structured logging.
- P0: CI build/test.
- P0: seed command.
- P0: README запуска.
- P1: OpenTelemetry.
- P1: architecture tests.

## Epic B. UI-shell

- P0: header.
- P0: search bar.
- P0: city selector.
- P0: category chips.
- P0: quick filters.
- P0: listing card.
- P0: listing grid.
- P0: fake map.
- P0: responsive layout.
- P0: CSS tokens.
- P0: hover card/map link.
- P1: dark theme.
- P1: component gallery.
- P1: visual regression.

## Epic C. Identity

- P0: user entity.
- P0: email OTP fake provider.
- P0: login/logout.
- P0: profile.
- P0: privacy settings.
- P1: phone OTP provider adapter.
- P1: sessions.
- P1: 2FA admin.
- P2: KYC adapter.
- P2: child account.

## Epic D. Catalog

- P0: category tree.
- P0: category admin.
- P0: dynamic attributes.
- P0: schema versions.
- P0: demo category packs.
- P1: dictionaries and dependencies.
- P1: comparison templates.
- P2: bulk category import/export.

## Epic E. Listings

- P0: draft.
- P0: three-step wizard.
- P0: media upload.
- P0: preview.
- P0: revision model.
- P0: price modes.
- P0: active quota.
- P0: my listings.
- P1: auto renew.
- P1: price history.
- P1: category recreation workflow.

## Epic F. Moderation

- P0: moderation cases.
- P0: basic text rules.
- P0: moderator queue.
- P0: decision reasons.
- P0: correction workflow.
- P0: audit.
- P1: OCR.
- P1: image classification adapter.
- P1: duplicate detection.
- P1: risk profile.
- P1: appeals.
- P2: trusted auto publish.

## Epic G. Search

- P0: search projection.
- P0: full-text search.
- P0: trigram typo tolerance.
- P0: common filters.
- P0: dynamic filters.
- P0: sorting.
- P0: pagination.
- P1: saved searches.
- P1: synonyms admin.
- P2: OpenSearch evaluation.
- Future: image search.

## Epic H. Geo

- P0: city selection.
- P0: browser geolocation.
- P0: IP adapter.
- P0: PostGIS location.
- P0: 30 km radius.
- P0: approximate private points.
- P1: Yandex adapter.
- P1: clusters.
- P1: map/list synchronization.
- P1: subway.
- P2: multiple pickup points.

## Epic I. Messaging

- P0: thread per listing and pair.
- P0: text message.
- P0: SignalR delivery.
- P0: read receipts.
- P0: typing and presence.
- P0: attachment upload.
- P0: complaints and block.
- P1: templates.
- P1: auto reply.
- P2: message search.
- Future: voice and transcription.
- Future: WebRTC calls.

## Epic J. Trust and engagement

- P0: favorites.
- P0: compare.
- P1: deal confirmation.
- P1: reviews.
- P1: price drop notification.
- P1: saved searches.
- P1: rule-based recommendations.
- P2: personalized ranking.

## Epic K. Admin and operations

- P0: role/permission model.
- P0: user search.
- P0: listing admin.
- P0: category admin.
- P0: moderation admin.
- P0: audit viewer.
- P1: feature flags.
- P1: jobs dashboard.
- P1: analytics dashboard.
- P2: support CRM integration.

## Epic L. Monetization

- P1: plans and entitlements.
- P1: promotion products.
- P1: promo codes.
- P1: bonus ledger.
- P1: fake payment adapter.
- P2: real provider.
- P2: refunds.
- P2: documents.
- Future: safe deal.
- Future: delivery.
