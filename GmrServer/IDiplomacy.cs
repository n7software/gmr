using GmrLib.Models;
using GmrServer.Models;
using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace GmrServer
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IDiplomacy" in both code and config file together.
    [ServiceContract]
    public interface IDiplomacy
    {
        [OperationContract]
        long? AuthenticateUser(string authKey);

        [OperationContract]
        List<PackagedPlayer> GetSteamPlayers(List<long> playerIDs);

        [OperationContract]
        List<PackagedGame> GetGamesForPlayer(string authKey);

        [OperationContract]
        SubmitTurnResult SubmitTurn(string authKey, int turnId, byte[] saveFileBytes);

        [OperationContract]
        SaveUploadResponse SubmitTurnStream(SaveUpload request);

        [OperationContract]
        SaveUploadResponse SubmitTurnStreamCompressed(SaveUpload request);

        [OperationContract]
        byte[] GetLatestSaveFileBytes(string authKey, int gameId);

        [OperationContract]
        SaveDownload GetLatestSaveFileBytesStream(SaveDownloadRequest request);

        [OperationContract]
        SaveDownload GetLatestSaveFileBytesStreamCompressed(SaveDownloadRequest request);

        [OperationContract]
        DateTime GetLatestSaveFileModifiedDate(string authKey, int gameId);
    }
}
