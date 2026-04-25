# Import reconciliation

Branche: `codex/import-reconciliation`

Objectif: eviter que plusieurs exports Binance representant le meme mouvement gonflent le portefeuille.

## Regle produit

Il vaut mieux bloquer un mouvement ambigu que l'ecrire deux fois.

## Ce qui change

- Les evenements Binance portent maintenant:
  - `SourceKind`: `TransactionHistory`, `SpotOrder`, `AlphaOrder`, `SpotTrade`, `AutoInvest`
  - `ExternalId`: numero d'ordre quand Binance le fournit
  - `Pair`: paire de trading quand disponible
- La preview reconcilie les evenements avant affichage.
- Les doublons probables sont retires de la liste active avant ecriture SQLite.
- L'upload est limite a 10 fichiers par session de selection.
- Les fichiers Auto-Invest vides affichent un message explicite.
- La colonne `Prix moyen` de la preview devient `Prix execution`.
- La table import affiche aussi la source et l'ID ordre.

## Strategie de dedoublonnage

1. Si un numero d'ordre Binance existe, il sert de cle prioritaire.
2. Sinon, une cle de mouvement est construite avec:
   - date execution
   - BUY/SELL
   - actif
   - quantite
   - devise de contrepartie
   - montant de contrepartie
3. Si deux sources decrivent le meme mouvement, l'app garde la source la plus precise:
   - `SpotTrade`
   - `SpotOrder` / `AlphaOrder`
   - `AutoInvest`
   - `TransactionHistory`

## Limites

- Les rewards et converts restent volontairement hors ecriture automatique.
- Les frais payes dans une autre crypto restent suivis mais pas encore convertis.
- Le ledger existant peut deja contenir d'anciens doublons importes avant cette branche: il faudra une future action de nettoyage/audit.

## Prochaine amelioration

Ajouter une vue `Quarantaine` pour afficher les doublons retires, avec la raison et la source conservee.
