# UX design decision

Date: 2026-04-25
Branche: `codex/ux-design`

## Decision

La trajectoire UX officielle est:

- design moderne WPF, mais pas de deuxieme app
- pas de web, Electron, WebView ou localhost
- l'interface doit expliquer les chiffres, pas les masquer
- la verite portefeuille reste le ledger SQLite local
- les tables restent disponibles pour l'audit, pas comme experience principale

## Dashboard

Le dashboard reste sobre tant que les prix live, le cache marche et le switch EUR/USDT ne sont pas clarifies.

Afficher en priorite:

- actifs suivis
- transactions ledger
- cout suivi hors prix live
- PnL realise
- frais suivis
- confiance data

Ne pas afficher une valeur totale portefeuille comme certaine tant que les prix marche ne sont pas une brique separee.

## Confiance data

Utiliser des etats lisibles plutot qu'un score abstrait:

- OK
- A verifier
- Doublon probable
- Import incomplet
- Vente superieure a la position
- Prix/frais ambigus

## Ordre de travail

1. Shell WPF propre: sidebar, Dashboard, Positions, Import Studio, Donnees.
2. Positions en cartes denses, basees sur les calculs existants.
3. Import Studio ameliore: filtres, selection, doublons, ignores, validation.
4. Detail actif X-Ray: resume, calcul, transactions, imports, anomalies.
5. Ensuite seulement: staging persistant, cache prix, graphiques.

## Etape UX actuelle

Passe UX v2:

- `MainWindow` devient un shell de coordination.
- L'UI est decoupee en vues:
  - `DashboardView`
  - `PositionsView`
  - `ImportStudioView`
  - `DataView`
- Les briques reutilisables sont dans `Controls`:
  - `MetricCard`
  - `AssetPositionCard`
  - `AssetChip`
  - `StatusBadge`
- Le dashboard affiche `Portefeuille valide SQLite`.
- L'import affiche `Preview Binance non validee`.
- Les positions signalent les devises mixtes comme `Devise mixte` / `Non consolide`.
- L'Import Studio a des chips actifs cliquables.
- Les doublons retires par reconciliation sont visibles en `Quarantaine`.
- Les graphiques restent ledger-only:
  - cout suivi par devise
  - volume par actif
  - PnL realise par actif
- La navigation a ete separee en sections visibles une par une:
  - `Tableau de bord`
  - groupe `Actifs` avec `Apercu ledger`, `Spot / Positions`, `Earn / Rewards`, `Alpha`
  - groupe `Ordres` avec `Import Studio`, `Journal ledger`
  - `Donnees`
- Le menu suit une logique type Binance: groupes repliables, sous-entrees explicites, contexte unique a l'ecran.
- Les graphiques ledger simples sont replies par defaut pour ne pas alourdir l'experience principale.
- Les cartes positions et metriques ont une premiere passe responsive: moins de colonnes fixes, textes non tronques.

Pas de changement volontaire sur:

- schema SQLite
- calcul portefeuille
- import Binance
- ecriture ledger
- API Binance
- prix live/cache marche

## Prochaine limite a traiter

La passe UX v2 ne convertit pas les devises. Si EUR, USDC et USDT coexistent, l'app affiche l'incertitude au lieu de fabriquer un total. La prochaine etape data doit ajouter une vraie strategie de devise de reference et un cache de taux avant tout switch EUR/USDT.
