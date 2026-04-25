# Revue UX cockpit

Branche cible: `codex/ux-cockpit-review`

Objectif: clarifier les ameliorations produit avant d'attaquer une nouvelle feature. Cette note sert de backlog trie: quoi faire, pourquoi, dans quelle branche, et ce qu'il faut eviter de casser.

## Diagnostic court

L'app sait maintenant lire beaucoup d'exports Binance, mais l'ecran ne raconte pas encore clairement l'etat du portefeuille.

Problemes principaux:

- le solde total / valeur portefeuille n'est pas visible en premiere intention
- `Volume trade` est utile en contexte import, mais pas comme metrique principale d'accueil
- les blocs sont encore trop techniques et trop proches d'une preview developpeur
- les imports, les actifs, les ordres et les donnees vivent encore trop dans le meme flux visuel
- les tableaux donnent du detail, mais pas une comprehension rapide
- les actions futures doivent rester visibles sans ecrire silencieusement dans SQLite

## Principe produit

La page d'accueil doit repondre en moins de 5 secondes a:

1. Qu'est-ce que j'ai ?
2. Combien j'ai investi ?
3. Qu'est-ce qui est fiable ou a verifier ?
4. Quels actifs expliquent l'essentiel du portefeuille ?
5. Quel import ou quelle anomalie demande mon attention ?

Tant qu'on n'a pas de prix marche fiable, on ne doit pas mentir avec une fausse valeur totale. On peut afficher:

- `Cout investi`
- `Valeur estimee`: indisponible ou partielle
- `PnL realise`
- `Frais suivis`
- `Transactions ledger`
- `Imports en attente`

## Branches recommandees

### 1. `codex/ux-cockpit`

But: refaire la page d'accueil pour qu'elle soit lisible et utile.

Contenu:

- remplacer la metrique centrale `Volume trade` par un bloc portefeuille
- afficher les KPI dans le bon ordre:
  - valeur estimee, si prix dispo
  - cout investi
  - PnL realise
  - frais
  - transactions ledger
  - imports en attente
- ajouter une section `A verifier`:
  - lignes pending
  - rewards non mappees
  - converts incomplets
  - doublons probables
- garder `Volume trade` dans la page Imports, pas dans l'accueil

Ce qu'on supprime/deplace:

- metriques d'import hors contexte
- tableaux longs en haut d'ecran
- libelles techniques visibles trop tot

Critere de validation:

- au lancement, on comprend si le portefeuille est vide, partiel ou fiable
- aucun chiffre derive ne semble magique
- aucune ecriture SQLite ajoutee dans cette branche

### 2. `codex/design-shell`

But: creer une navigation plus moderne et plus respirable.

Contenu:

- sidebar stable
- pages:
  - Tableau de bord
  - Actifs
  - Ordres
  - Imports
  - Donnees
- volets repliables pour detail actif / detail ordre
- transitions legeres ouverture-fermeture
- styles WPF propres pour boutons, tabs, inputs et DataGrid

Decision technique:

- rester WPF natif d'abord
- animations simples avec `Storyboard`
- eviter une dependance UI tant qu'un style XAML interne suffit

Critere de validation:

- l'app ne ressemble plus a une page unique empilee
- navigation utilisable en 1280x720
- pas de conflit avec le moteur import/calcul

### 3. `codex/import-ledger-mapper`

But: faire passer les imports valides dans le portefeuille.

Contenu:

- creer `BinanceLedgerMapper`
- transformer `BinanceImportEvent` en `LedgerTransaction` candidate
- dedoublonnage persistant avant ecriture
- preview `sera ecrit / bloque / ignore`
- bouton `Valider dans le portefeuille`

Ce qu'on garde strict:

- pas d'ecriture silencieuse
- audit des raisons
- backup SQLite avant gros import si besoin

Critere de validation:

- apres validation, le portefeuille n'affiche plus 0 si des trades valides existent
- CSV et XLSX equivalents ne doublent pas les transactions

### 4. `codex/asset-xray`

But: une fiche actif qui explique les chiffres.

Contenu:

- fiche ETH/BTC/etc
- quantite
- prix moyen achat
- prix moyen vente
- cout investi
- frais
- PnL realise
- historique achats/ventes
- detail d'un ordre au clic
- bloc `Pourquoi ce chiffre ?`

Critere de validation:

- depuis un actif, on peut remonter aux transactions sources
- le calcul du prix moyen est comprehensible

### 5. `codex/data-confidence`

But: transformer les incertitudes en workflow produit.

Contenu:

- score par import:
  - complet
  - a verifier
  - partiel
  - bloque
- badges par evenement:
  - importable
  - reward a confirmer
  - interne ignore
  - cash hors PnL
  - convert ambigu
- vue anomalies

Critere de validation:

- l'utilisateur sait quoi corriger
- les erreurs ne sont plus seulement du texte technique en bas d'ecran

### 6. `codex/build-workflow`

But: rendre les builds moins ambigus.

Contenu:

- `build-current.bat`
- sortie par branche:
  - `release/localCrypto-dev`
  - `release/localCrypto-master`
  - `release/localCrypto-codex-...`
- `release/localCrypto` reserve a master stable
- doc courte dans `docs/BUILD.md`

Critere de validation:

- on sait toujours quel exe correspond a quelle branche
- pas besoin de retenir une commande `dotnet publish`

### 7. `codex/market-data`

But: ajouter la valeur estimee et les graphiques sans polluer la verite ledger.

Prerequis:

- mapping import -> ledger termine
- actifs reconstruits proprement

Contenu:

- cache prix local
- taux EUR/USD
- PnL latent
- graphiques 1D, 1M, 6M, YTD, 1Y
- comparaison multi-actifs

Regle:

- les prix marche ne modifient jamais `transactions`

## Ordre recommande

1. `codex/build-workflow`
2. `codex/import-ledger-mapper`
3. `codex/ux-cockpit`
4. `codex/design-shell`
5. `codex/asset-xray`
6. `codex/data-confidence`
7. `codex/market-data`

Raison:

- le build propre evite la confusion pendant les tests
- le mapping ledger debloque le vrai solde / portefeuille
- le cockpit devient utile quand il a des donnees fiables
- le design dynamique vient ensuite, pour mieux explorer ces donnees

## Premiere feature conseillee

Je recommande `codex/import-ledger-mapper` avant le design pur.

Pourquoi:

- le probleme du solde total vient d'abord du fait que les imports restent en preview
- si on embellit trop avant d'ecrire les transactions, l'UI restera vide ou trompeuse
- une fois le portefeuille alimente, l'UX cockpit aura des vrais chiffres a organiser

Limite:

- faire seulement le mapping BUY/SELL simple au premier passage
- garder CONVERT/REWARD en `a confirmer`
- ne pas toucher aux charts
