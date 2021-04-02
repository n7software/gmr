using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GmrLib.Models
{
    using System.Data.Entity;
    using System.Linq;

    public partial class GmrEntities
    {
        public override int SaveChanges()
        {
            var entriesDeleted = GetChangedEntries(EntityState.Deleted);

            foreach (var entry in entriesDeleted)
            {
                var save = entry as Save;
                if (save != null)
                {
                    if (File.Exists(save.FilePath))
                    {
                        File.Delete(save.FilePath);
                    }
                }
                else
                {
                    var game = entry as Game;
                    if (game != null)
                    {
                        if (File.Exists(game.UnalteredSaveFilePath))
                        {
                            File.Delete(game.UnalteredSaveFilePath);
                        }
                    }
                }
            }

            WriteOutFilesForSaves(GetChangedEntries(EntityState.Modified));
            WriteOutUnalteredSaveForGames(GetChangedEntries(EntityState.Modified | EntityState.Unchanged));

            var entriesAdded = GetChangedEntries(EntityState.Added);

            int ret = base.SaveChanges();

            WriteOutFilesForSaves(entriesAdded);
            WriteOutUnalteredSaveForGames(entriesAdded);

            return ret;
        }

        private IEnumerable<object> GetChangedEntries(EntityState entityState)
        {
            return
                (from entry in ChangeTracker.Entries()
                 where entry.Entity != null
                       && entry.State == entityState
                 select entry.Entity).ToArray();
        }

        private void WriteOutFilesForSaves(IEnumerable<object> entriesAddedOrModified)
        {
            foreach (object entry in entriesAddedOrModified)
            {
                var save = entry as Save;
                if (save != null)
                {
                    Directory.CreateDirectory(Save.SaveDir);
                    if (save.Data != null)
                    {
                        File.WriteAllBytes(save.FilePath, save.Data);
                    }
                }
            }
        }

        private void WriteOutUnalteredSaveForGames(IEnumerable<object> entriesAddedOrModified)
        {
            foreach (object entry in entriesAddedOrModified)
            {
                var turn = entry as Turn;
                if (turn != null)
                {
                    var game = turn.Game;
                    Directory.CreateDirectory(Game.UnalteredSaveDir);

                    bool writeSuccessfull = false;
                    int attempts = 0;

                    do
                    {
                        try
                        {
                            if (game.UnalteredSaveFileBytes != null)
                            {
                                File.WriteAllBytes(game.UnalteredSaveFilePath, game.UnalteredSaveFileBytes);
                                writeSuccessfull = true;
                            }
                        }
                        catch
                        {
                            Thread.Sleep(100);
                        }

                        attempts++;
                    } while (!writeSuccessfull && attempts < 10);
                }
            }
        }

        public static GmrEntities CreateContext()
        {
            var gmrDb = new GmrEntities();

            gmrDb.Database.CommandTimeout = 300;

            return gmrDb;
        }
    }
}