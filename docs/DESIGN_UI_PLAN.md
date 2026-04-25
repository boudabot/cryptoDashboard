# Plan design UI

Branche cible: `codex/design-ui`

Objectif: transformer l'app WPF en vrai produit desktop finance local-first, avec une ergonomie proche Binance / Perplexity Finance, sans web, sans localhost, sans Electron.

## Priorite produit

La priorite n'est pas seulement de rendre l'app plus jolie. Il faut rendre l'app plus lisible et plus fiable:

1. voir rapidement l'etat du portefeuille
2. comprendre d'ou viennent les chiffres
3. importer plusieurs fichiers sans doublons
4. naviguer par actif, ordre et import
5. garder une separation stricte entre verite portefeuille et donnees de marche

## Diagnostic actuel

- L'app fonctionne, mais l'ecran principal empile trop de blocs.
- L'import Binance est utile, mais doit devenir une page dediee.
- La saisie manuelle n'est plus le flux principal.
- Les donnees importees doivent alimenter le portefeuille, pas rester une preview isolee.
- Les tableaux sont encore trop bruts pour un usage quotidien.
- Les actifs doivent devenir des pages consultables: resume, trades, prix moyen, ventes, frais, PnL, graphiques.
- Les controles WPF par defaut donnent une impression technique: dropdown blanc, tabs bruts, tableaux trop denses.
- Le portefeuille affiche encore 0 tant que l'import preview n'ecrit pas dans SQLite.
- Le detail utile est au mauvais niveau: on voit trop de lignes, mais pas assez de synthese.

## Pages cibles

- Tableau de bord: valeur estimee, allocation, mouvements recents, alertes d'import.
- Actifs: vue portefeuille lisible, triable par actif, quantite, prix moyen, cout investi, PnL, frais.
- Detail actif: fiche ETH/BTC/etc avec transactions liees, moyennes achat/vente, ordres, frais, explication des calculs.
- Ordres: trades regroupes, filtres BUY/SELL/CONVERT/REWARD/INTERNAL, detail au clic.
- Imports: depot multi-fichiers, historique des imports, erreurs, doublons, validation controlee.
- Donnees: SQLite, backup, restore, audit, chemin source de verite.

## Trajectoire ergonomique

### Etape 1 - Shell applicatif

Creer une structure fixe:

- sidebar sombre
- zone contenu principale
- navigation claire:
  - Tableau de bord
  - Actifs
  - Ordres
  - Imports
  - Donnees
  - Parametres

But: ne plus tout empiler dans une seule page scrollable.

Critere de validation:

- aucune section principale n'est cachee dans un grand scroll unique
- la navigation reste visible
- l'app reste utilisable en fenetre 1280x720

### Etape 2 - Page Imports

Deplacer tout l'import Binance dans une page dediee.

Fonctions:

- bouton `Ajouter exports`
- selection multi-fichiers CSV/XLSX
- recap imports charges
- deduplication visible
- onglets:
  - Resume
  - Actifs detectes
  - Ordres groupables
  - A confirmer
  - Rejets
- bouton futur `Valider dans le portefeuille`

But: l'import est un workflow controle, pas une table technique.

Regle importante:

- charger un CSV et le XLSX equivalent ne doit pas doubler les donnees dans la preview
- chaque fichier charge doit apparaitre dans une liste d'exports
- chaque evenement importe doit avoir une signature stable pour la detection de doublon

### Etape 3 - Page Actifs

Afficher une liste style portefeuille Binance:

- logo/pastille actif
- symbole
- nom
- quantite
- valeur estimee
- prix moyen
- cout investi
- PnL latent
- PnL realise
- frais
- badge Alpha/Earn si utile

Chaque actif ouvre une vue detail.

Les lignes doivent etre lisibles sans ouvrir un tableau technique. Le tableau detaille reste disponible, mais pas comme premier ecran.

### Etape 4 - Detail actif

Exemple ETH:

- total ETH
- prix moyen achat
- prix moyen vente
- cout investi
- frais
- PnL realise
- PnL latent quand prix marche disponible
- historique des achats/ventes
- detail d'un ordre au clic:
  - date
  - type BUY/SELL
  - quantite
  - prix moyen
  - total quote
  - frais
  - source import

But: comprendre pourquoi une position vaut ce qu'elle vaut.

Calculs a rendre explicables:

- prix moyen achat
- prix moyen vente
- cout investi
- frais inclus
- PnL realise
- mouvements ignores car internes
- lignes a confirmer avant ecriture

### Etape 5 - Graphiques

Ajouter une page ou un panneau marche:

- courbe prix actif
- comparaison multi-actifs
- timeframes: 1D, 1M, 6M, YTD, 1Y
- marqueurs achats/ventes
- later: correlation entre actifs

Les donnees marche doivent etre separees de la verite portefeuille:

- `transactions`: verite portefeuille
- `market_prices`: cache prix
- `fx_rates`: EUR/USD
- `asset_metadata`: noms/logos/tags

Ne pas melanger les prix marche avec `transactions`. Une erreur de prix live ne doit jamais modifier l'historique portefeuille.

## Bibliotheques candidates

### UI WPF

Option recommandee: styles WPF custom d'abord, puis MaterialDesignInXAML si besoin.

Pourquoi:

- WPF natif reste simple a packager.
- On evite de transformer l'app en stack web.
- MaterialDesignInXAML fournit des styles pour les controles WPF standard, des cartes, dialogues, icones et transitions.

Source: https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit

Decision initiale:

- commencer avec styles XAML internes
- n'ajouter MaterialDesignInXAML que si les controles natifs coutent trop cher a rendre propres
- eviter MahApps/Dragablz au depart pour ne pas multiplier les concepts

### Graphiques

Option A: ScottPlot WPF

- simple
- bon pour courbes, scatter, series temporelles
- integration WPF directe via `ScottPlot.WPF`

Source: https://scottplot.net/quickstart/wpf/

Option B: LiveChartsCore

- plus visuel et anime
- plus adapte aux dashboards modernes
- package WPF: `LiveChartsCore.SkiaSharpView.WPF`

Source: https://livecharts.dev/docs/wpf/2.0.0-rc2/overview.installation

Decision initiale:

- choisir ScottPlot si priorite robustesse/simple courbes
- choisir LiveChartsCore si priorite rendu dashboard moderne
- ne pas installer les deux
- prototype recommande: LiveChartsCore pour le rendu dashboard, puis ScottPlot seulement si les chandeliers ou gros volumes de points deviennent prioritaires

### Tableaux et listes

Decision initiale: pas de nouvelle bibliotheque.

- utiliser `DataGrid` WPF stylise pour les vues techniques
- utiliser des `ItemsControl` / cartes-lignes pour les vues portefeuille
- garder les tableaux complets derriere un onglet detail

### Navigation

Decision initiale: WPF natif.

- sidebar fixe
- pages internes via contenu controle
- pas de framework MVVM lourd tant que l'app reste petite
- extraire progressivement des petits view models si le code-behind devient trop dense

### Logos crypto

Premiere etape:

- pastilles texte locales, ex: BTC, ETH, SOL

Etape suivante:

- table `asset_metadata`
- logos locaux caches
- pas de dependance reseau pour afficher le portefeuille

## Regles design

- UI sombre principale, cartes sobres, accents jaune Binance + vert/rouge marche.
- Pas de gros tableaux par defaut.
- Les tableaux servent au detail, pas au premier niveau.
- Les actions dangereuses doivent demander confirmation.
- Les donnees importees doivent toujours expliquer leur origine.
- Les imports ne doivent jamais ecrire silencieusement dans SQLite.

## Ordre de realisation propose

1. Creer shell + navigation sidebar.
2. Deplacer Imports dans une page dediee.
3. Transformer Actifs en vraie page portefeuille.
4. Ajouter detail actif avec ordres lies.
5. Ajouter ecriture SQLite controlee depuis import valide.
6. Ajouter graphiques prix/ordre.
7. Ajouter metadata actifs/logos.

## Plan d'execution par petites branches internes

Sur la branche `codex/design-ui`, travailler par checkpoints:

1. `Shell`: sidebar + pages + styles de base.
2. `Imports`: page dediee, multi-fichiers, liste exports, resume clair.
3. `Portfolio`: actifs reconstruits depuis transactions SQLite.
4. `Asset detail`: detail actif + ordres lies + calculs expliques.
5. `Import write`: validation vers SQLite avec doublons et audit.
6. `Charts`: premier graphique local avec donnees de prix separees.

Chaque checkpoint doit garder:

- `.\test-app.bat` vert
- `.\build-app.bat` vert
- executable testable dans `release\localCrypto\localCrypto.exe`
- donnees locales dans `%APPDATA%\localCrypto\localcrypto.sqlite`

## Prochaine etape immediate

Implementer uniquement l'etape 1:

- shell applicatif
- navigation sidebar
- pages vides ou deplacement minimal des blocs existants
- garder app buildable et testable

Ne pas commencer les graphiques avant que la navigation et la structure de pages soient propres.
