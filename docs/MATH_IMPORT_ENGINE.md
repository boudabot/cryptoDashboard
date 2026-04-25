# Moteur import et calcul portefeuille

Branche cible: `codex/math-import-engine`

Objectif: transformer les exports Binance en evenements portefeuille propres, dedoublonnables et explicables, avant toute ecriture SQLite.

## Exports Binance pris en compte

| Export | CSV | XLSX | Etat | Role |
| --- | --- | --- | --- | --- |
| Historique des transactions | Oui | Oui | Deja couvert | Source large: achats, ventes, frais, Earn, Alpha, mouvements internes, cash |
| Historique d'ordre Spot | Oui | Oui | Deja couvert | Ordres executes avec prix moyen Binance |
| Historique des ordre Alpha | Oui | Oui | Deja couvert | Ordres Alpha executes avec prix moyen Binance |
| Historique des trades Spot | Oui | Oui | Ajoute | Trades executes avec prix, montant et frais directs |
| Auto-Invest | Oui | Oui | Ajoute | Achats recurrents; fichiers vides acceptes sans erreur |

## Regle produit

L'app doit accepter plusieurs exports dans une meme session. Charger le CSV et le XLSX equivalent ne doit pas doubler les evenements.

La preview doit rester sans ecriture SQLite tant que l'utilisateur n'a pas valide l'import.

## Modele mathematique cible

### Evenement import

Un export Binance est converti en evenements normalises:

- `BUY`
- `SELL`
- `CONVERT`
- `REWARD`
- `INTERNAL`
- `CASH`
- `UNKNOWN`

Chaque evenement conserve:

- date execution
- actif achete/vendu
- quantite
- devise de contrepartie
- montant contrepartie
- prix unitaire
- frais
- devise de frais
- nombre de lignes source
- statut: importable, a confirmer, ignore, rejet
- raison lisible

### Mapping ledger

Seuls les evenements expliques doivent devenir des `transactions`.

- `BUY`: augmente la position, augmente le cout investi.
- `SELL`: diminue la position, calcule le PnL realise via prix moyen.
- `CONVERT`: doit etre confirme si l'actif source et l'actif cible ne sont pas ambigus.
- `REWARD`: doit devenir revenu/reward, mais il faut une regle comptable explicite avant ecriture.
- `INTERNAL`: ignore pour PnL, utile pour audit.
- `CASH`: garde hors PnL trade.
- `UNKNOWN`: jamais ecrit automatiquement.

## Ce qui est deja en place

- Detection CSV/XLSX.
- Parsing des lignes XLSX avec en-tetes decales.
- Regroupement des lignes de transaction Binance en evenements.
- Lecture des ordres Spot/Alpha executes.
- Lecture des trades Spot.
- Lecture Auto-Invest, y compris export vide.
- Dedoublonnage en memoire dans l'UI pendant une session d'upload.
- Calcul portefeuille depuis `transactions`: quantite, cout investi, prix moyen, frais, PnL realise.

## Ce qui manque encore

- Ecriture controlee des evenements importables vers SQLite.
- Tables d'audit d'import: session, fichier, evenement, lignes source.
- Dedoublonnage persistant entre sessions.
- Justification detaillee par transaction apres import.
- Gestion comptable propre des rewards Earn/Alpha.
- Conversion des frais payes dans une crypto differente de la devise de contrepartie.
- PnL latent: necessite un cache prix marche separe.

## Prochaine etape recommandee

Creer un service `BinanceLedgerMapper` separe du previewer.

Responsabilites:

- prendre des `BinanceImportEvent`
- produire des `LedgerTransaction` candidates
- expliquer pourquoi une candidate est ecrite, ignoree ou bloquee
- ne jamais modifier SQLite directement

Ensuite seulement, ajouter une action UI `Valider dans le portefeuille`.
