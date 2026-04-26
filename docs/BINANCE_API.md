# Binance API read-only

Date: 2026-04-26
Branche de travail initiale: `codex/binance-api-readonly`

## Decision

On ajoute Binance API comme source observable separee du ledger SQLite.

Elle sert a:

- lire le solde Spot courant
- lire les positions Simple Earn flexible/locked quand Binance les expose a la cle
- lire les ordres Spot ouverts
- verifier une cle API read-only
- recuperer des prix publics indicatifs
- alimenter un cache local de prix, de bougies et de snapshots courts pour les futurs graphes

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

Regle produit: Binance declare; le ledger SQLite valide et explique.

## Documentation officielle consultee

- Binance Spot API README: https://developers.binance.com/docs/binance-spot-api-docs/README
- REST API Spot: https://developers.binance.com/docs/binance-spot-api-docs/rest-api

Points retenus:

- les endpoints publics comme `/api/v3/time` et `/api/v3/ticker/price` ne demandent pas de signature
- les endpoints `USER_DATA`, dont `/api/v3/account`, demandent `X-MBX-APIKEY`, `timestamp`, `recvWindow` et une signature HMAC SHA256
- `/api/v3/account` donne les balances Spot du compte
- `/api/v3/openOrders` donne les ordres Spot ouverts
- `/api/v3/ticker/price` donne un prix public par symbole, par exemple `ETHUSDT`
- `/api/v3/klines` donne des bougies de marche par symbole et intervalle
- `/sapi/v1/account/apiRestrictions` permet de lire les permissions de la cle API courante avant de l'accepter
- `/sapi/v1/simple-earn/account` donne une synthese Simple Earn
- `/sapi/v1/simple-earn/flexible/position` donne les positions Earn flexibles
- `/sapi/v1/simple-earn/locked/position` donne les positions Earn verrouillees

## Cache local Binance

Les donnees live Binance ne sont pas ecrites dans la table `transactions`.

Elles sont cachees dans des tables separees du meme fichier SQLite:

- `binance_asset_snapshots`
- `binance_open_orders_current`
- `binance_price_snapshots`
- `binance_klines`

Ces tables servent a:

- garder une trace locale courte des refreshs API
- preparer les graphes sans tout rappeler a chaque affichage
- separer clairement `ledger valide` et `source Binance observable`
- faciliter une future reconciliation API -> preview -> validation ledger

Elles ne servent pas a calculer le portefeuille officiel. La source de verite portefeuille reste `transactions`.

Hygiene cache:

- `binance_asset_snapshots` et `binance_price_snapshots` gardent seulement un historique court, actuellement 30 jours
- `binance_open_orders_current` garde uniquement le dernier etat observe des ordres ouverts
- `binance_klines` fait un upsert par bougie, symbole et intervalle
- le bouton `Purger cache Binance` supprime uniquement les tables `binance_*`
- la purge cache ne supprime ni `transactions`, ni la cle API locale
- une sauvegarde de `localcrypto.sqlite` contient aussi ces donnees Binance observables; elle doit donc etre protegee comme une donnee personnelle sensible

## Securite locale

La cle API doit etre creee cote Binance avec permissions lecture seulement.

Dans l'app:

- stockage local: `%APPDATA%\localCrypto\binance-readonly.dat`
- secret chiffre avec la protection Windows utilisateur courant
- aucune permission trading ou transfert n'est requise
- la cle et le secret ne sont pas stockes dans Git
- la cle et le secret ne sont pas stockes dans SQLite
- la cle et le secret ne sont pas ecrits dans les docs ou les logs
- apres sauvegarde, les champs de saisie sont vides
- le secret n'est jamais reaffiche
- les messages d'erreur Binance sont limites et nettoyes avant affichage
- avant sauvegarde, l'app appelle `/sapi/v1/account/apiRestrictions`
- la cle est refusee si une permission dangereuse est activee:
  - retrait
  - transfert interne
  - transfert universel
  - margin
  - futures
  - options
  - FIX trading
  - spot/margin trading
  - portfolio margin trading
- la cle stockee doit avoir `enableReading=true` et les permissions dangereuses a `false`

## Limites de securite explicites

DPAPI `CurrentUser` protege contre la lecture simple du fichier par un autre utilisateur Windows. Ce n'est pas un coffre inviolable contre un logiciel malveillant qui tournerait deja sous le meme compte Windows.

Regle d'exploitation:

- ne jamais envoyer la cle API ou le secret dans un chat
- ne jamais capturer l'ecran pendant que les champs sont remplis
- creer la cle en lecture seule
- supprimer la cle cote Binance si elle a ete exposee
- ne pas demander a un assistant ou a un script de dechiffrer `%APPDATA%\localCrypto\binance-readonly.dat`
- faire tourner les vraies cles seulement via l'UI de l'app, pas via tests ou scripts de debug
- utiliser une cle dediee facile a supprimer/renouveler cote Binance

## Limites connues

- pas encore de conversion officielle EUR/USDT
- les prix publics USDT sont indicatifs
- certains actifs peuvent ne pas avoir de paire `{ASSET}USDT`
- les actifs `LD...` sont mappes vers leur sous-jacent pour le prix public quand c'est possible
- Alpha et Auto-Invest demandent encore des endpoints Binance separes ou restent mieux couverts par exports au debut
- les graphes temps reel doivent passer plus tard par WebSocket ou refresh controle; cette passe stocke deja les snapshots/klines
