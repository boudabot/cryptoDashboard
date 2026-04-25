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

Premiere passe:

- fond sombre coherent
- KPI dashboard plus sobres
- suppression de la table positions comme experience principale
- positions affichees en cartes denses
- panneau Asset X-Ray a droite
- journal ledger garde comme table d'audit

Pas de changement volontaire sur:

- schema SQLite
- calcul portefeuille
- import Binance
- ecriture ledger
