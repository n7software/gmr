using GmrLib;
using GmrLib.Models;
using GmrWorker;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;

namespace GmrServer.Controllers
{
    public class DiplomacyController : ApiController
    {
        [HttpGet, HttpPost]
        public long? AuthenticateUser(string authKey)
        {
            DesktopAppMonitor.Instance.OnMessageFromAuthTokenReceived(authKey);

            try
            {
                using (var gmrDB = GmrEntities.CreateContext())
                {
                    return GameManager.GetUserIdFromAuthKey(authKey, gmrDB);
                }
            }
            catch { return null; }
        }

        [HttpGet, HttpPost]
        public List<PackagedPlayer> GetSteamPlayers(List<long> playerIDs)
        {
            return PackagedPlayers(playerIDs);
        }

        [HttpGet, HttpPost]
        public List<PackagedGame> GetGamesForPlayer(string authKey)
        {
            DesktopAppMonitor.Instance.OnMessageFromAuthTokenReceived(authKey);

            long userId;

            return GamesForPlayer(authKey, out userId);
        }

        [HttpGet, HttpPost]
        public GamesAndPlayers GetGamesAndPlayers(string playerIDText, string authKey)
        {
            DesktopAppMonitor.Instance.OnMessageFromAuthTokenReceived(authKey);

            try
            {
                List<long> playerIDs = new List<long>();

                if (!string.IsNullOrWhiteSpace(playerIDText))
                {
                    playerIDs = new List<string>(playerIDText.Split('_')).ConvertAll(long.Parse);
                }

                long userId;

                var result = new GamesAndPlayers
                {
                    Games = GamesForPlayer(authKey, out userId),
                    Players = PackagedPlayers(playerIDs),
                    CurrentTotalPoints = PlayerStatsManager.Instance.GetStatsForPlayer(userId).TotalPoints
                };

                return result;
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "GetGamesAndPlayers");
                throw;
            }
        }

        [HttpGet, HttpPost]
        public DateTime GetLatestSaveFileModifiedDate(string authKey, int gameId)
        {
            DesktopAppMonitor.Instance.OnMessageFromAuthTokenReceived(authKey);

            try
            {
                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    var player = gmrDb.Users.FirstOrDefault(u => u.AuthKey == authKey);
                    if (player != null)
                    {
                        var game = player.GamePlayers.Where(gp => gp.GameID == gameId)
                                               .Select(gp => gp.Game)
                                               .FirstOrDefault();
                        if (game != null)
                        {
                            return GameManager.GetLatestSaveModified(game);
                        }

                    }
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, string.Format("Diplomacy: Get modified date for latest save by gameId: {0}", gameId));
            }

            return DateTime.UtcNow;
        }

        [HttpGet, HttpPost]
        public HttpResponseMessage GetLatestSaveFileBytes(string authKey, int gameId)
        {
            DesktopAppMonitor.Instance.OnMessageFromAuthTokenReceived(authKey);

            try
            {
                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    var player = gmrDb.Users.FirstOrDefault(u => u.AuthKey == authKey);
                    if (player != null)
                    {
                        var game = player.GamePlayers.Where(gp => gp.GameID == gameId)
                                               .Select(gp => gp.Game)
                                               .FirstOrDefault();
                        if (game != null)
                        {
                            byte[] saveFileBytes = GameManager.GetLatestSaveFileBytes(game);

                            HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.OK);
                            response.Content = new StreamContent(new MemoryStream(saveFileBytes));
                            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                            response.Content.Headers.ContentDisposition.FileName = Global.SaveFileDownloadName;
                            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                            response.Content.Headers.ContentLength = saveFileBytes.Length;

                            return response;
                        }
                        else
                        {
                            throw new HttpResponseException(HttpStatusCode.NotFound);
                        }
                    }
                    else
                    {
                        throw new HttpResponseException(HttpStatusCode.Unauthorized);
                    }
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, string.Format("Diplomacy: Getting latest save file bytes for gameId: {0}", gameId));

                throw new HttpResponseException(HttpStatusCode.InternalServerError);
            }
        }

        [HttpGet, HttpPost]
        public HttpResponseMessage GetLatestSaveFileBytesCompressed(string authKey, int gameId)
        {
            DesktopAppMonitor.Instance.OnMessageFromAuthTokenReceived(authKey);

            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var player = gmrDb.Users.FirstOrDefault(u => u.AuthKey == authKey);
                if (player != null)
                {
                    var game = player.GamePlayers.Where(gp => gp.GameID == gameId)
                                           .Select(gp => gp.Game)
                                           .FirstOrDefault();
                    if (game != null)
                    {
                        try
                        {
                            byte[] saveFileBytes =
                                CivSaveLib.Compresion.CompressBytes(GameManager.GetLatestSaveFileBytes(game));

                            HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.OK);
                            response.Content = new StreamContent(new MemoryStream(saveFileBytes));
                            response.Content.Headers.ContentDisposition =
                                new ContentDispositionHeaderValue("attachment");
                            response.Content.Headers.ContentDisposition.FileName = Global.SaveFileDownloadName;
                            response.Content.Headers.ContentType =
                                new MediaTypeHeaderValue("application/octet-stream");
                            response.Content.Headers.ContentLength = saveFileBytes.Length;

                            return response;
                        }
                        catch (Exception exc)
                        {
                            DebugLogger.WriteException(exc,
                                string.Format("Diplomacy: Getting latest save file bytes for gameId: {0}", gameId));

                            throw new HttpResponseException(HttpStatusCode.InternalServerError);
                        }
                    }
                    else
                    {
                        throw new HttpResponseException(HttpStatusCode.NotFound);
                    }
                }
                else
                {
                    throw new HttpResponseException(HttpStatusCode.Unauthorized);
                }
            }
        }

        [HttpGet, HttpPost]
        public SubmitTurnResult SubmitTurn(string authKey, int turnId)
        {
            DesktopAppMonitor.Instance.OnMessageFromAuthTokenReceived(authKey);

            var result = new SubmitTurnResult();

            SubmitTurnInternal(authKey, turnId, false, result);

            return result;
        }

        [HttpGet, HttpPost]
        public SubmitTurnResult SubmitTurnCompressed(string authKey, int turnId)
        {
            DesktopAppMonitor.Instance.OnMessageFromAuthTokenReceived(authKey);

            var result = new SubmitTurnResult();

            SubmitTurnInternal(authKey, turnId, true, result);

            return result;
        }

        private void SubmitTurnInternal(string authKey, int turnId, bool bytesCompressed, SubmitTurnResult result)
        {
            try
            {
                bool worked = false;
                byte[] saveFileBytes = null;

                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    var player = gmrDb.Users.FirstOrDefault(u => u.AuthKey == authKey);
                    if (player != null)
                    {
                        var turn = player.Turns.FirstOrDefault(t => t.TurnID == turnId);
                        if (turn != null && !turn.Finished.HasValue)
                        {
                            saveFileBytes = GetSaveFileBytesFromRequest();

                            if (saveFileBytes.Length != Request.Content.Headers.ContentLength.Value)
                            {
                                throw new Exception(
                                    string.Format(
                                        "Byte array length ({0}) not equal to HTTP content-length header ({1}). This is not good!",
                                        saveFileBytes.Length, this.Request.Content.Headers.ContentLength.Value));
                            }

                            if (bytesCompressed)
                            {
                                saveFileBytes = CivSaveLib.Compresion.DecompressBytes(saveFileBytes);
                            }

                            int pointsEarned = 0;

                            worked = GameManager.SubmitTurn(turn, gmrDb, out pointsEarned);

                            result.PointsEarned = pointsEarned;

                            if (worked)
                            {
                                gmrDb.SaveChanges();

                                PlayerStatsManager.Instance.RemoveUserStats(player.UserId);
                            }
                        }
                    }
                }

                if (worked)
                {
                    Task.Factory.StartNew<bool>(
                        GameManager.FinishSubmitTurnThread,
                        new FinishTurnArgs
                        {
                            SaveFileBytes = saveFileBytes,
                            TurnId = turnId,
                            SetHumanPlayers = true
                        })
                        .ContinueWith(t => GC.Collect());
                }

                result.ResultType = worked ? SubmitTurnResultType.OK : SubmitTurnResultType.UnexpectedError;
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, string.Format("Diplomacy: Sumbitting turn for turnId: {0}", turnId));

                result.ResultType = SubmitTurnResultType.UnexpectedError;
            }
            finally
            {
                GC.Collect();
            }
        }

        private byte[] GetSaveFileBytesFromRequest()
        {
            var t = Request.Content.ReadAsByteArrayAsync();
            t.Wait();
            return t.Result;
        }


        private static List<PackagedGame> GamesForPlayer(string authKey, out long userId)
        {
            userId = -1;

            try
            {
                using (var gmrDB = GmrEntities.CreateContext())
                {
                    userId = GameManager.GetUserIdFromAuthKey(authKey, gmrDB) ?? -1;
                    if (userId > 0)
                    {
                        return GameManager.GetGamesForPlayer(userId, gmrDB);
                    }
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, string.Format("Diplomacy: Getting games for player authKey: {0}", authKey));
            }

            return new List<PackagedGame>();
        }

        private static List<PackagedPlayer> PackagedPlayers(List<long> playerIDs)
        {
            try
            {
                if (playerIDs != null)
                {
                    Global.SteamApiInstance.RequestUserPolling(playerIDs);

                    var players = Global.SteamApiInstance.GetAllCachedPlayers(playerIDs);

                    return players.ConvertAll<PackagedPlayer>(p => new PackagedPlayer(p));
                }
                else
                {
                    return new List<PackagedPlayer>();
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Diplomacy: Getting steam players");
                return new List<PackagedPlayer>();
            }
        }
    }

    public class GamesAndPlayers
    {
        public List<PackagedGame> Games { get; set; }
        public List<PackagedPlayer> Players { get; set; }

        public int CurrentTotalPoints { get; set; }

        public GamesAndPlayers()
        {

        }
    }
}
