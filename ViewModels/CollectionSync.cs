using System.Collections.ObjectModel;

namespace Nexus.App.ViewModels;

internal static class CollectionSync
{
    /// <summary>Met à jour les lignes existantes sur place (évite le scintillement de l'interface).</summary>
    public static void Sync<TRow, TSource>(
        ObservableCollection<TRow> rows,
        IReadOnlyList<TSource> source,
        Func<TSource, TRow> create,
        Action<TRow, TSource> update)
    {
        while (rows.Count > source.Count)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        for (var i = 0; i < source.Count; i++)
        {
            if (i >= rows.Count)
            {
                rows.Add(create(source[i]));
            }

            update(rows[i], source[i]);
        }
    }
}