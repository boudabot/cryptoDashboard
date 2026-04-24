# Donnees locales

## Source de verite

La source de verite V0.1 est le ledger SQLite:

```text
%APPDATA%\localCrypto\localcrypto.sqlite
```

Les positions et le PnL realise sont recalcules depuis les transactions. Aucune valeur derivee n'est stockee comme verite.

## Schema V0.1

Table principale, compatible avec le prototype Electron existant:

```text
transactions
```

Champs:

- id
- executed_at
- side
- symbol
- asset_name
- quantity
- unit_price
- quote_currency
- fee_amount
- fee_currency
- source
- note
- created_at

## Sauvegarde

1. Fermer localCrypto.
2. Copier `%APPDATA%\localCrypto\localcrypto.sqlite`.
3. Garder la copie dans un dossier de backup.

## Restauration

1. Fermer localCrypto.
2. Renommer la base actuelle si elle existe.
3. Copier la sauvegarde vers `%APPDATA%\localCrypto\localcrypto.sqlite`.
4. Relancer l'application.

## Migration

Toute evolution de schema devra etre explicite et versionnee avant de devenir V1.
