# Binance API read-only

Date: 2026-04-26
Branche de travail initiale: `codex/binance-api-readonly`

## Decision

On ajoute Binance API comme source live separee du ledger SQLite.

Elle sert a:

- lire le solde Spot courant
- verifier une cle API read-only
- recuperer des prix publics indicatifs
- preparer le futur cache prix/devise

Elle ne sert pas encore a:

- ecrire automatiquement dans `transactions`
- remplacer les exports CSV/XLSX historiques
- calculer un PnL latent officiel
- passer des ordres
- transferer des fonds

## Pourquoi les imports restent utiles

L'API Spot donne une vue courante et certains historiques Spot. Les exports restent utiles pour reconstruire un historique complet, auditable, incluant les fichiers Binance deja utilises:

- historique des transactions
- ordres Spot
- trades Spot
- ordres Alpha
- Auto-Invest
- Earn/rewards

Regle produit: l'API live enrichit; le ledger SQLite explique.

## Documentation officielle consultee

- Binance Spot API README: https://developers.binance.com/docs/binance-spot-api-docs/README
- REST API Spot: https://developers.binance.com/docs/binance-spot-api-docs/rest-api

Points retenus:

- les endpoints publics comme `/api/v3/time` et `/api/v3/ticker/price` ne demandent pas de signature
- les endpoints `USER_DATA`, dont `/api/v3/account`, demandent `X-MBX-APIKEY`, `timestamp`, `recvWindow` et une signature HMAC SHA256
- `/api/v3/account` donne les balances Spot du compte
- `/api/v3/ticker/price` donne un prix public par symbole, par exemple `ETHUSDT`

## Securite locale

La cle API doit etre creee cote Binance avec permissions lecture seulement.

Dans l'app:

- stockage local: `%APPDATA%\localCrypto\binance-readonly.dat`
- secret chiffre avec la protection Windows utilisateur courant
- aucune permission trading ou transfert n'est requise

## Limites connues

- pas encore de cache prix persistant
- pas encore de conversion officielle EUR/USDT
- les prix publics USDT sont indicatifs
- certains actifs peuvent ne pas avoir de paire `{ASSET}USDT`
- Simple Earn, Alpha et Auto-Invest peuvent demander des endpoints Binance separes ou rester mieux couverts par exports au debut
