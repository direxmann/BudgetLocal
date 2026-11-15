# Guide utilisateur BudgetLocal

## Démarrer

1. Copiez `Distribution\BudgetLocal-Setup.exe` sur le PC Windows, puis ouvrez-le.
2. Suivez la demande d’installation et ouvrez BudgetLocal depuis le raccourci créé sur le Bureau.
3. Ajoutez une transaction en saisissant le montant, le libellé, la catégorie, le type et la date.

Les données restent sur l’ordinateur dans `%LocalAppData%\BudgetLocal\budgetlocal.db`. Les sauvegardes sont conservées dans `%LocalAppData%\BudgetLocal\Backups`. La distribution ne contient pas la base utilisateur.

## Budgets

Dans la barre latérale, choisissez le budget actif ou ouvrez **Créer / gérer les budgets**. Un budget contient son propre nom, une description facultative, son plafond mensuel, ses transactions et ses statistiques. Un nouveau budget devient actif dès sa création. Les opérations et graphiques affichés concernent uniquement le budget sélectionné.

Un budget avec des transactions ne peut pas être supprimé afin d’éviter toute perte de données. Il n’est pas possible de supprimer le dernier budget.

## Transactions

La section **Ajouter une transaction** ne sert qu’à créer une nouvelle opération. Les transactions sont enregistrées individuellement : plusieurs dépenses peuvent partager la même catégorie et conserver leurs identifiants propres.

Dans **Transactions**, combinez le texte recherché, le type, la catégorie et les dates. Le menu **Trier par** permet un tri croissant ou décroissant par date ou montant. Les boutons **Modifier** et **Supprimer** sont disponibles sur chaque ligne. La modification s’ouvre dans une fenêtre indépendante; **Annuler** abandonne les changements non enregistrés. Une suppression demande une confirmation.

Les sélecteurs de date sont agrandis et partagés entre l’ajout, les filtres et la fenêtre de modification.

## Soldes et plafond mensuel

Le **solde total** est calculé comme tous les revenus du budget actif moins toutes ses dépenses. Le **budget mensuel restant** est son plafond de dépenses moins les dépenses du mois en cours. Le plafond est facultatif et aucun montant n’est supposé par l’application. Sans plafond défini, le montant restant apparaît comme non défini. Si les dépenses dépassent le plafond, le montant restant devient négatif.

La page **Graphiques** contient la répartition par catégorie, la comparaison des revenus et dépenses par mois, ainsi que le solde total et le budget restant. Les périodes proposées comprennent l’année en cours, l’année précédente, les 12 derniers mois, les 30 derniers jours et une période personnalisée.

## Réinitialiser le budget

Le bouton **Réinitialiser mon budget** demande confirmation et propose une sauvegarde préalable. Il efface uniquement les transactions. Les catégories par défaut, le plafond mensuel, les autres paramètres et les sauvegardes existantes sont conservés. Cette opération est définitive si aucune sauvegarde préalable n’est créée.

## Import et export

Les imports et exports concernent uniquement le budget actif. Un import crée d’abord une sauvegarde complète, remplace les transactions de ce budget et conserve les autres budgets. Une sauvegarde manuelle de la base complète est aussi accessible depuis le menu latéral.

L’installateur vise Windows 10/11 64 bits. Il installe l’application avec son runtime .NET 10 intégré; il n’est pas nécessaire d’installer .NET 10 ou le SDK.

