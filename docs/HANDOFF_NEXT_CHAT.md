# Handoff next chat

Date: 2026-04-25

But: permettre a une nouvelle conversation de reprendre le projet sans perdre le contexte produit, Git, donnees et UX.

## Regle de maintenance

Ce document est le point de reprise court du projet. Il doit etre mis a jour apres chaque etape structurante:

- merge d'une branche feature dans `dev`
- creation ou suppression d'une branche importante
- changement du workflow build/release
- changement du schema SQLite ou de la source de verite
- nouvelle limite connue sur les imports, le calcul portefeuille ou l'UX
- decision produit qui remplace une ancienne direction

Il ne doit pas devenir un journal complet. Garder une version courte, actionnable et compressee:

- etat reel du repo
- decisions encore valides
- risques connus
- prochaine branche recommandee
- commandes exactes pour reprendre

Si le contenu devient trop long, archiver le detail dans un document dedie et garder ici seulement le resume et le lien.

## Etat Git actuel

Repo principal:

```text
C:\Users\1oliv\Documents\localCryptoWpf-spike
```

Branche active au moment de cette note:

```text
codex/ux-design
```

Derniers commits connus:

```text
cfd27b9 Add portfolio UX redesign pass
5490e82 Update handoff after dev merge
2c31dbc Document handoff maintenance rule
6dc3195 Add next chat handoff
f2d268c Add ledger reset action
```

`dev` contient `codex/import-reconciliation`.
`master` pointe encore sur `1faa400` tant que la release stable n'a pas ete validee.
`codex/ux-design` contient la refonte UX en cours.

Il n'y a pas de remote configure au moment de cette note.

## Trajectoire produit

Application cible:

- Windows native WPF + C# + SQLite
- pas de web
- pas d'Electron
- pas de localhost
- usage final: double-clic sur `localCrypto.exe`

Source de verite portefeuille:

```text
%APPDATA%\localCrypto\localcrypto.sqlite
```

Table principale:

```text
transactions
```

Les imports Binance ne doivent jamais devenir une deuxieme source de verite. Ils servent a construire ou expliquer des transactions ledger.

## Build et releases

Script principal:

```bat
build-current.bat
```

Le script detecte la branche courante:

- `master` -> `release\localCrypto`
- `dev` -> `release\localCrypto-dev`
- `codex/import-reconciliation` -> `release\localCrypto-codex-import-reconciliation`

Regle pratique: ne pas lancer deux builds vers le meme dossier si un exe de ce dossier est ouvert.

## Etat fonctionnel

Ce qui existe deja:

- backup / restore SQLite
- suppression transaction avec confirmation
- bouton `Vider ledger`
- import Binance CSV/XLSX
- preview Binance transaction history, spot orders, alpha orders, spot trades, auto-invest
- validation explicite des BUY/SELL importables vers SQLite
- dedoublonnage preview/ecriture sur `codex/import-reconciliation`
- limite upload: 10 fichiers par selection
- build par branche
- tests unitaires sur import, portfolio, clear ledger
- UI decoupee en vues/controles WPF sur `codex/ux-design`
- navigation UX separee par sections, avec groupes sidebar repliables type Binance
- separation visuelle ledger valide / preview Binance
- chips actifs import cliquables
- quarantaine import en memoire pour doublons probables
- graphiques ledger simples sans prix live, replies par defaut

Ce qui reste incomplet ou a corriger:

- le ledger SQLite peut deja contenir des doublons issus d'imports faits avant la branche reconciliation
- les valeurs portfolio peuvent etre fausses si la base existante n'a pas ete videe puis reimportee proprement
- les devises mixtes sont maintenant signalees comme non consolidees, mais pas encore converties
- pas encore de switch global EUR / USDT
- pas encore de prix live/cache marche
- pas encore de PnL latent fiable
- `CONVERT`, `REWARD`, `INTERNAL`, `CASH` ne sont pas ecrits automatiquement dans le ledger
- pas encore de vraie quarantaine persistante des imports
- pas encore de preuve detaillee type "Pourquoi ce chiffre ?"
- le polish responsive reste a affiner apres test utilisateur: petites largeurs, tables detail, densite des panneaux

Regle importante: il vaut mieux manquer une ligne ambigue que l'ecrire deux fois.

## Reponse au dossier UX lab

Dossier externe:

```text
C:\Users\1oliv\Documents\localCrypto-ux-lab
```

Lecture:

- le lab UX est utile comme cadrage produit
- il ne doit pas devenir une deuxieme app
- il ne doit pas remplacer le repo WPF
- ses wireframes doivent etre reintegres par petites branches dans le repo principal

Ce qui est bon dans la proposition:

- concept `Portfolio Command Center`
- separation `Positions`, `Import Studio`, `Transactions`, `Donnees`
- tables reservees au detail/audit, pas a l'experience principale
- panneau detail actif avec preuve ledger
- `Import Studio` comme espace de reconciliation, pas simple preview temporaire
- logos crypto locaux avec fallback par initiales
- pas de prix marche tant que le ledger n'est pas fiable

Ce qu'il faut corriger ou temporiser:

- ne pas ajouter tout de suite des tables staging si le flux UX n'est pas valide
- ne pas faire une refonte massive de `MainWindow` et du calcul en meme temps
- ne pas afficher une "valeur totale" comme certaine sans cache prix et devise de base claire
- ne pas importer des milliers d'icones; commencer par les actifs reels
- ne pas transformer les warnings en simple texte bas de page; il faut un workflow lisible

Recommendation pour l'autre chat design:

```text
Travaille uniquement comme UX lab / specs / wireframes. Ne modifie pas le repo principal sans demande explicite.
Priorise un shell WPF realiste: navigation, panneaux, cartes actifs, detail progressif.
Respecte la verite ledger SQLite et ne propose pas de deuxieme source de donnees.
```

## Structure recommandee ensuite

Ordre conseille apres validation de `codex/import-reconciliation`:

1. Merge `codex/import-reconciliation` dans `dev`, puis dans `master` si test utilisateur OK.
2. `codex/import-audit-cleanup`
   - afficher doublons ignores
   - mieux expliquer les imports
   - aider a repartir d'un ledger propre
3. `codex/portfolio-cards`
   - remplacer le tableau positions par des cartes actifs
   - ne pas changer les calculs
4. `codex/import-studio-v0`
   - filtres
   - selection multiple
   - detail event
   - quarantaine non persistante au depart
5. `codex/portfolio-breakdown`
   - DTO de calcul explicable
   - "Pourquoi ce chiffre ?"
   - tests unitaires prix moyen / PnL realise / frais
6. `codex/import-staging-persistent`
   - tables `import_batches`, `import_rows`, `import_events`, `import_decisions`
   - seulement si le flux Import Studio est valide
7. `codex/market-fx-cache`
   - cache prix marche separe du ledger
   - taux EUR/USD/USDT
   - switch affichage EUR / USDT

## Directive pour nouvelle conversation

Demander au prochain chat de commencer par:

```bat
git status --short --branch
git log --oneline --decorate -5
```

Puis lire dans cet ordre:

```text
docs\HANDOFF_NEXT_CHAT.md
docs\IMPORT_RECONCILIATION.md
docs\PORTFOLIO_TRUTH_UPGRADE.md
docs\BUILD.md
docs\DATA.md
```

Regles de travail:

- toujours partir de `dev` pour une nouvelle feature
- creer une branche `codex/...`
- garder le projet buildable apres chaque etape
- ne pas toucher a `master` sauf release stable
- ne pas faire de refactor massif sans validation
- ne pas melanger verite ledger, staging import et donnees marche
- utiliser `build-current.bat` pour produire un exe de test

## Prompt court pour reprendre

```text
Tu reprends le projet localCrypto WPF dans C:\Users\1oliv\Documents\localCryptoWpf-spike.
Lis docs\HANDOFF_NEXT_CHAT.md puis verifie git status.
Trajectoire: WPF + C# + SQLite local-first, pas de web, pas Electron, pas localhost.
Source de verite: %APPDATA%\localCrypto\localcrypto.sqlite, table transactions.
Ne fais pas de refactor massif.
Travaille depuis dev sur une branche codex/... et garde le build runnable.
Priorite produit: imports Binance surs, dedoublonnage, portefeuille explicable, UI lisible.
Avant de coder: dis quelle branche tu proposes et ce que tu modifies.
```
