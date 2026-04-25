# Portfolio truth upgrade

Branche: `codex/portfolio-truth-upgrade`

Objectif: livrer une premiere version integree des 7 axes sans casser la trajectoire WPF locale.

## 1. Build workflow

- Ajout de `build-current.bat`.
- La sortie depend de la branche active.
- `master` genere `release\localCrypto`.
- Les branches `codex/...` generent `release\localCrypto-codex-...`.

## 2. Import vers ledger

- Ajout de `BinanceLedgerMapper`.
- Les evenements Binance `BUY` et `SELL` importables deviennent des `LedgerTransaction` candidates.
- `CONVERT`, `REWARD`, `INTERNAL`, `CASH` restent bloques ou en quarantaine.
- L'ecriture SQLite passe par confirmation utilisateur.
- Les doublons SQLite restent refuses par `SqliteLedgerStore`.

## 3. UX cockpit

- Ajout d'un bloc `Portefeuille suivi`.
- `Volume trade` reste dans Imports et n'est plus le chiffre qui pilote l'accueil.
- Ajout d'un indicateur `Confiance`.
- Le solde affiche le cout suivi hors prix live pour ne pas mentir sur la valeur marche.

## 4. Design shell

- Ajout d'une navigation laterale simple:
  - Tableau de bord
  - Actifs
  - Imports
  - Donnees
- Les boutons naviguent vers les zones de la page.
- Les animations/volets avances restent a faire dans une future branche design dediee.

## 5. Asset X-Ray

- Selectionner une position affiche un panneau de detail.
- Le panneau liste:
  - quantite
  - prix moyen
  - cout investi
  - PnL realise
  - transactions sources

## 6. Data confidence

- L'accueil indique si le ledger est `OK` ou `Partiel`.
- Les imports pending/rejetes/ignores alimentent l'indication.
- Les warnings du `PortfolioCalculator` restent visibles.

## 7. Market data foundation

- Aucun prix live n'est encore branche.
- L'UI indique explicitement `hors prix live`.
- La prochaine etape marche doit ajouter un cache separe de `transactions`.

## Limites volontaires

- Pas de prix live dans cette branche.
- Pas de chart reel tant que les positions ne sont pas correctement alimentees.
- Pas d'ecriture automatique pour `CONVERT` et `REWARD`.
- Pas de grosse refonte MVVM.

## Prochaine branche recommandee

`codex/design-shell-motion`

But:

- vrais volets repliables
- transitions legeres
- pages separees plus propres
- meilleur style DataGrid/TabControl
