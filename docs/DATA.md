# Donnees locales

## Source de verite

La source de verite V0.1.1 est le ledger SQLite:

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

## Sauvegarde recommandee

1. Ouvrir localCrypto.
2. Aller au bloc `Donnees`.
3. Cliquer `Sauvegarder SQLite`.
4. Garder le fichier `.sqlite` produit dans un dossier de backup.

Alternative manuelle: fermer localCrypto, copier `%APPDATA%\localCrypto\localcrypto.sqlite`, puis garder cette copie dans un dossier de backup.

## Restauration recommandee

1. Ouvrir localCrypto.
2. Aller au bloc `Donnees`.
3. Cliquer `Restaurer SQLite`.
4. Choisir la sauvegarde.
5. Confirmer le remplacement de la base active.

Alternative manuelle: fermer localCrypto, remplacer `%APPDATA%\localCrypto\localcrypto.sqlite`, supprimer les fichiers `localcrypto.sqlite-wal` et `localcrypto.sqlite-shm` s'ils existent, puis relancer.

## Doublons et suppression

Les transactions ont une signature de doublon basee sur date, side, symbole, quantite, prix, frais et devise de frais. Un doublon probable est refuse avec un message lisible.

La suppression se fait depuis le journal, ligne par ligne, avec confirmation. Les positions et PnL sont ensuite recalcules depuis les transactions restantes.

## Legacy

L'ancien cache Electron n'est pas la source de verite du flux WPF. Il doit rester hors du flux principal tant que la trajectoire WPF est validee.

## Migration

Toute evolution de schema devra etre explicite et versionnee avant de devenir V1.
