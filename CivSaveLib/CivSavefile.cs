using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CivSaveLib
{
    public class CivSavefile
    {
        #region Constants

        public enum CivDifficulty
        {
            AiDefault = 1,
            Prince = 3
        }

        public enum PlayerType
        {
            Human = 3,
            AI = 1,
            Nobody = 2,
            Unknown = 4
        }

        public enum SaveFileType
        {
            SinglePlayer = 0,
            Multiplayer = 1,
            HotSeat = 2
        }

        protected const int BytesToSkipInBeginning = 56;
        protected const byte Section_Delimiter = 0x40;

        protected const int NameSectionNumber = 2;
        protected const int NameSectionNumberSecondary = 21;
        protected const int PasswordSectionNumber = 12;
        protected const int CivilizationSectionNumber = 7;
        protected const int LeaderSectionNumber = 8;
        protected const int CurrentPlayerIndexSectionNumber = LeaderSectionNumber;
        protected const int PlayerColorSectionNumber = 24;
        protected const int DifficultySectionNumber = 16;
        protected const int PlayerTypeSectionNumber = 27;
        protected const int GameTypeSectionNumber = 15;
        protected const int MysteriousDisappearingSection = 17;
        protected const int MapScriptSectionNumber = 18;
        protected const int GameTypeHeaderLocation = 0x2C;
        protected static byte[] StringPadding = { 0x00, 0x00, 0x00 };

        protected static byte[] BegginingSaveFileBytes = { 0x43, 0x49, 0x56, 0x35 };
        protected static byte[] BytesForLUA = { 0x6C, 0x75, 0x61 };
        protected static byte[] BytesForMAP = { 0x4D, 0x61, 0x70 };
        protected static byte[] DisappearingSectionLastBlock = { 0xFF, 0xFF, 0x00, 0x00 };

        protected static long Patch674BuildNumber = 310700L;

        protected static List<char> invalidNameChars = new List<char> { '[', ']' };

        protected static Dictionary<int, int> sectionsToSkip = new Dictionary<int, int>
        {
            // {<section number>, <number of bytes to skip>}

            {PasswordSectionNumber, 0x100},
            {NameSectionNumber, 0x100},
            {NameSectionNumberSecondary, 0x100},
            {GameTypeSectionNumber, 0x12A},
            {MapScriptSectionNumber, 0x112}
        };

        #endregion

        #region Base Methods

        protected static byte[] GetSaveFileSectionBytes(byte[] saveFileBytes, int sectionNumber)
        {
            var sectionBytes = new List<byte>();

            try
            {
                int sectionCount = 0;
                int sectionStart = -1;
                int readIndex = BytesToSkipInBeginning;

                while (readIndex < saveFileBytes.Length)
                {
                    if (saveFileBytes[readIndex] == Section_Delimiter)
                    {
                        bool foundText = true;
                        int textLength = BytesForLUA.Length;

                        for (int i = 0; i < textLength; i++)
                        {
                            foundText &= (saveFileBytes[readIndex - (textLength - i)] == BytesForLUA[i]
                                          || saveFileBytes[readIndex - (textLength - i)] == BytesForMAP[i]);
                        }

                        if (foundText)
                        {
                            break;
                        }
                    }

                    readIndex++;
                }

                while (readIndex < saveFileBytes.Length)
                {
                    if (saveFileBytes[readIndex] == Section_Delimiter)
                    {
                        sectionCount++;

                        if (sectionCount == sectionNumber)
                        {
                            sectionStart = ++readIndex;
                            break;
                        }

                        //Crazy special case where a section is missing
                        if (sectionCount == MysteriousDisappearingSection + 1)
                        {
                            if (!PreceedingSectionIsDisappearingOne(saveFileBytes, readIndex))
                            {
                                readIndex += BlocksToSkipInSection(MysteriousDisappearingSection + 1);
                                sectionCount++;
                            }
                        }

                        readIndex += BlocksToSkipInSection(sectionCount);
                    }

                    readIndex++;
                }

                if (sectionStart > -1)
                {
                    int sectionIndex = 0,
                        sectionLimit = sectionsToSkip.ContainsKey(sectionCount) ? sectionsToSkip[sectionCount] : 0;

                    while (saveFileBytes[readIndex] != Section_Delimiter ||
                           sectionIndex < sectionLimit)
                    {
                        sectionBytes.Add(saveFileBytes[readIndex++]);
                        sectionIndex++;
                    }
                }
            }
            catch
            {
            }

            return sectionBytes.ToArray();

        }

        private static bool PreceedingSectionIsDisappearingOne(byte[] saveFileBytes, int readIndex)
        {
            byte[] preceedingBlock = new byte[4];
            Array.ConstrainedCopy(saveFileBytes, readIndex - 4, preceedingBlock, 0, 4);
            return preceedingBlock.SequenceEqual(DisappearingSectionLastBlock);
        }

        private static int BlocksToSkipInSection(int sectionCount)
        {
            if (sectionsToSkip.ContainsKey(sectionCount))
                return sectionsToSkip[sectionCount];
            else return 0;
        }

        protected static byte[] SetSaveFileSectionBytes(byte[] saveFileBytes, byte[] sectionBytes, int sectionNumber)
        {
            var newFileBytes = new List<byte>();

            try
            {
                int sectionCount = 0;
                int sectionStart = -1;
                int readIndex = 0;

                while (readIndex < BytesToSkipInBeginning)
                {
                    newFileBytes.Add(saveFileBytes[readIndex++]);
                }

                while (readIndex < saveFileBytes.Length)
                {
                    if (saveFileBytes[readIndex] == Section_Delimiter)
                    {
                        bool foundText = true;
                        int textLength = BytesForLUA.Length;

                        for (int i = 0; i < textLength; i++)
                        {
                            foundText &= (saveFileBytes[readIndex - (textLength - i)] == BytesForLUA[i]
                                          || saveFileBytes[readIndex - (textLength - i)] == BytesForMAP[i]);
                        }

                        if (foundText)
                        {
                            break;
                        }
                    }

                    newFileBytes.Add(saveFileBytes[readIndex]);

                    readIndex++;
                }

                while (readIndex < saveFileBytes.Length)
                {
                    newFileBytes.Add(saveFileBytes[readIndex]);

                    if (saveFileBytes[readIndex] == Section_Delimiter)
                    {
                        sectionCount++;

                        if (sectionCount == sectionNumber)
                        {
                            sectionStart = ++readIndex;
                            break;
                        }

                        //Crazy special case where a section is missing
                        if (sectionCount == MysteriousDisappearingSection + 1)
                        {
                            if (!PreceedingSectionIsDisappearingOne(saveFileBytes, readIndex))
                            {
                                readIndex = AddSkippedBlock(saveFileBytes, newFileBytes, sectionCount, readIndex);
                                sectionCount++;
                            }
                        }

                        readIndex = AddSkippedBlock(saveFileBytes, newFileBytes, sectionCount, readIndex);
                    }

                    readIndex++;
                }

                if (sectionStart > -1)
                {
                    newFileBytes.AddRange(sectionBytes);

                    int sectionIndex = 0,
                        sectionLimit = sectionsToSkip.ContainsKey(sectionCount) ? sectionsToSkip[sectionCount] : 0;

                    while (saveFileBytes[readIndex] != Section_Delimiter ||
                           sectionIndex < sectionLimit)
                    {
                        readIndex++;
                        sectionIndex++;
                    }
                }

                while (readIndex < saveFileBytes.Length)
                {
                    newFileBytes.Add(saveFileBytes[readIndex++]);
                }
            }
            catch
            {
            }

            return newFileBytes.ToArray();
            ;
        }

        private static int AddSkippedBlock(byte[] saveFileBytes, List<byte> newFileBytes, int sectionCount, int readIndex)
        {
            if (sectionsToSkip.ContainsKey(sectionCount))
            {
                for (int i = 0; i < sectionsToSkip[sectionCount]; i++)
                {
                    newFileBytes.Add(saveFileBytes[++readIndex]);
                }
            }
            return readIndex;
        }

        protected static string GetStringFromSaveFileBytes(byte[] saveFileBytes, int playerNumber, int sectionNumber)
        {
            string result = string.Empty;

            try
            {
                int strLength = 0;
                int readIndex = 3;

                byte[] sectionBytes = GetSaveFileSectionBytes(saveFileBytes, sectionNumber);

                if (sectionBytes.Length > 0)
                {
                    for (int i = 1; i < playerNumber; i++)
                    {
                        strLength = sectionBytes[readIndex];

                        readIndex += strLength + 4;
                    }

                    var resultBytes = new List<byte>();

                    strLength = sectionBytes[readIndex];
                    readIndex += 4;

                    for (int i = 0; i < strLength; i++)
                    {
                        resultBytes.Add(sectionBytes[readIndex + i]);
                    }

                    result = Encoding.ASCII.GetString(resultBytes.ToArray());
                }
            }
            catch
            {
            }

            return result;
        }

        protected static byte[] SetStringInSaveFileBytes(byte[] saveFileBytes, int playerNumber, string value,
            int sectionNumber)
        {
            try
            {
                int readIndex = 3;
                int strLength = 0;
                var newSectionBytes = new List<byte>();

                byte[] sectionBytes = GetSaveFileSectionBytes(saveFileBytes, sectionNumber);

                if (sectionBytes.Length > 0)
                {
                    newSectionBytes.AddRange(StringPadding);

                    for (int i = 1; i < playerNumber; i++)
                    {
                        strLength = sectionBytes[readIndex];

                        for (int j = 0; j < (strLength + 4); j++)
                        {
                            newSectionBytes.Add(sectionBytes[readIndex++]);
                        }
                    }

                    newSectionBytes.Add((byte)value.Length);
                    newSectionBytes.AddRange(StringPadding);
                    newSectionBytes.AddRange(Encoding.ASCII.GetBytes(value));

                    strLength = sectionBytes[readIndex];

                    for (int j = 0; j < (strLength + 4); j++)
                    {
                        readIndex++;
                    }

                    while (readIndex < sectionBytes.Length)
                    {
                        newSectionBytes.Add(sectionBytes[readIndex++]);
                    }

                    return SetSaveFileSectionBytes(saveFileBytes, newSectionBytes.ToArray(), sectionNumber);
                }
            }
            catch
            {
            }

            return saveFileBytes;
        }

        protected static byte GetByteInSaveFileBytes(byte[] saveFileBytes, int playerNumber, int sectionNumber)
        {
            byte result = 0;
            int readIndex = 3;

            byte[] sectionBytes = GetSaveFileSectionBytes(saveFileBytes, sectionNumber);

            if (sectionBytes.Length > 0)
            {
                readIndex += 4 * (playerNumber - 1);

                result = sectionBytes[readIndex];
            }

            return result;
        }

        protected static byte[] SetByteInSaveFileBytes(byte[] saveFileBytes, int playerNumber, byte value,
            int sectionNumber)
        {
            try
            {
                int readIndex = 3;
                var newSectionBytes = new List<byte>();

                byte[] sectionBytes = GetSaveFileSectionBytes(saveFileBytes, sectionNumber);

                if (sectionBytes.Length > 0)
                {
                    newSectionBytes.AddRange(StringPadding);

                    for (int currentPlayerNum = 1; currentPlayerNum < playerNumber; currentPlayerNum++)
                    {
                        for (int x = 0; x < 4; x++)
                        {
                            newSectionBytes.Add(sectionBytes[readIndex++]);
                        }
                    }
                    newSectionBytes.Add(value);
                    readIndex++;

                    while (readIndex < sectionBytes.Length)
                    {
                        newSectionBytes.Add(sectionBytes[readIndex++]);
                    }

                    return SetSaveFileSectionBytes(saveFileBytes, newSectionBytes.ToArray(), sectionNumber);
                }
            }
            catch
            {
            }

            return saveFileBytes;
        }


        protected static long GetVersionBuildNumberFromSaveFileBytes(byte[] saveFileBytes)
        {
            int index = 0x20;

            // Skip null values
            while (0x00 == saveFileBytes[index])
            {
                index++;
            }

            var bytes = new byte[6];

            for (int i = 0; i < 6; i++)
            {
                bytes[i] = saveFileBytes[index++];
            }

            return long.Parse(Encoding.ASCII.GetString(bytes));
        }

        public static bool AreBytesCivSaveFile(byte[] saveFileBytes)
        {
            for (int i = 0; i < BegginingSaveFileBytes.Length; i++)
            {
                if (saveFileBytes[i] != BegginingSaveFileBytes[i])
                    return false;
            }

            return true;
        }

        public static bool IsNewFileBytesBloated(int originalFileSize, int newFileSize)
        {
            return newFileSize > 1048576 // The new file is larger than 1MB
                   && newFileSize >= (originalFileSize * 1.2); // and it's larger than 120% of the original file
        }

        public static byte[] SetAllPlayerNamesToDefault(byte[] saveFileBytes)
        {
            string playerName;
            int playerNumber = 1;

            do
            {
                playerName = GetPlayerNameFromSaveFileBytes(saveFileBytes, playerNumber);
                if (!string.IsNullOrEmpty(playerName))
                {
                    saveFileBytes = SetPlayerNameInSaveFileBytes(saveFileBytes, playerNumber,
                        string.Format("Player {0}", playerNumber));
                    playerNumber++;
                }
            } while (!string.IsNullOrEmpty(playerName));

            playerNumber = 1;

            do
            {
                playerName = GetPlayerNameSecondaryFromSaveFileBytes(saveFileBytes, playerNumber);
                if (!string.IsNullOrEmpty(playerName))
                {
                    saveFileBytes = SetPlayerNameSecondaryInSaveFileBytes(saveFileBytes, playerNumber,
                        string.Format("Player {0}", playerNumber));
                    playerNumber++;
                }
            } while (!string.IsNullOrEmpty(playerName));

            return saveFileBytes;
        }

        #endregion

        #region Set Game Type

        public static byte[] SetGameTypeInSaveFileBytes(byte[] bytes, SaveFileType gameType)
        {
            var newBytes = new byte[bytes.Length];
            bytes.CopyTo(newBytes, 0);
            newBytes[GameTypeHeaderLocation] = (byte)gameType;

            byte[] gameTypeSectionBytes = GetSaveFileSectionBytes(newBytes, GameTypeSectionNumber);
            gameTypeSectionBytes[gameTypeSectionBytes.Length - 9] = (byte)gameType;
            newBytes = SetSaveFileSectionBytes(newBytes, gameTypeSectionBytes, GameTypeSectionNumber);

            return newBytes;
        }

        #endregion

        #region Get/Set Name

        public static string GetPlayerNameFromSaveFileBytes(byte[] saveFileBytes, int playerNumber)
        {
            return GetStringFromSaveFileBytes(saveFileBytes, playerNumber, NameSectionNumber);
        }

        public static byte[] SetPlayerNameInSaveFileBytes(byte[] saveFileBytes, int playerNumber, string name)
        {
            foreach (char c in invalidNameChars)
            {
                name = name.Replace(c, ' ');
            }

            return SetStringInSaveFileBytes(saveFileBytes, playerNumber, name, NameSectionNumber);
        }

        public static string GetPlayerNameSecondaryFromSaveFileBytes(byte[] saveFileBytes, int playerNumber)
        {
            return GetStringFromSaveFileBytes(saveFileBytes, playerNumber, NameSectionNumberSecondary);
        }

        public static byte[] SetPlayerNameSecondaryInSaveFileBytes(byte[] saveFileBytes, int playerNumber, string name)
        {
            foreach (char c in invalidNameChars)
            {
                name = name.Replace(c, ' ');
            }

            return SetStringInSaveFileBytes(saveFileBytes, playerNumber, name, NameSectionNumberSecondary);
        }

        #endregion

        #region Get/Set Password

        public static string GetPlayerPasswordFromSaveFileBytes(byte[] saveFileBytes, int playerNumber)
        {
            return GetStringFromSaveFileBytes(saveFileBytes, playerNumber, PasswordSectionNumber);
        }

        public static byte[] SetPlayerPasswordInSaveFileBytes(byte[] saveFileBytes, int playerNumber, string password)
        {
            return SetStringInSaveFileBytes(saveFileBytes, playerNumber, password, PasswordSectionNumber);
        }

        #endregion

        #region Get/Set Difficulty

        public static CivDifficulty GetPlayerDifficultyFromSaveFileBytes(byte[] saveFileBytes, int playerNumber)
        {
            return
                (CivDifficulty)
                    GetByteInSaveFileBytes(saveFileBytes, playerNumber, DifficultySectionNumber);
        }

        public static byte[] SetPlayerDifficultyInSaveFileBytes(byte[] saveFileBytes, int playerNumber,
            CivDifficulty difficulty)
        {
            return SetByteInSaveFileBytes(saveFileBytes, playerNumber, (byte)difficulty,
                DifficultySectionNumber);
        }

        #endregion

        #region Get/Set Type

        public static PlayerType GetPlayerTypeFromSaveFileBytes(byte[] saveFileBytes, int playerNumber)
        {
            return
                (PlayerType)
                    GetByteInSaveFileBytes(saveFileBytes, playerNumber, PlayerTypeSectionNumber);
        }

        public static byte[] SetPlayerTypeInSaveFileBytes(byte[] saveFileBytes, int playerNumber, PlayerType type)
        {
            return SetByteInSaveFileBytes(saveFileBytes, playerNumber, (byte)type, PlayerTypeSectionNumber);
        }

        #endregion

        #region Get/Set Current Player

        public static int GetCurrentPlayerInSaveFileBytes(byte[] saveFileBytes)
        {
            int section = GetCurrentPlayerSectionNumber(saveFileBytes);

            byte[] sectionBytes = GetSaveFileSectionBytes(saveFileBytes, section);

            int index = GetCurrentPlayerIndexForSectionBytes(sectionBytes);

            return sectionBytes[index];
        }

        public static byte[] SetCurrentPlayerInSaveFileBytes(byte[] saveFileBytes, int playerNumber)
        {
            int section = GetCurrentPlayerSectionNumber(saveFileBytes);

            byte[] sectionBytes = GetSaveFileSectionBytes(saveFileBytes, section);

            int index = GetCurrentPlayerIndexForSectionBytes(sectionBytes);

            sectionBytes[index] = (byte)(playerNumber);

            return SetSaveFileSectionBytes(saveFileBytes, sectionBytes, section);
        }

        private static int GetCurrentPlayerSectionNumber(byte[] saveFileBytes)
        {
            return CurrentPlayerIndexSectionNumber;
        }

        private static int GetCurrentPlayerIndexForSectionBytes(byte[] sectionBytes)
        {
            int index = sectionBytes.Length - 16;

            string block = String.Empty;
            while (block != "LEADER_BARBARIAN")
            {
                var newBlock = new StringBuilder();
                for (int i = 0; i < 16; i++)
                    newBlock.Append((char)sectionBytes[index + i]);
                block = newBlock.ToString();
                index--;
            }

            return index + 21;
        }

        #endregion

        #region Get Player Count

        public static int GetPlayerCount(byte[] saveFileBytes)
        {
            int currentIndex = 0;

            PlayerType type;
            do
            {
                currentIndex++;
                type = GetPlayerTypeFromSaveFileBytes(saveFileBytes, currentIndex);
            } while (type == PlayerType.AI || type == PlayerType.Human);

            return currentIndex - 1;
        }

        #endregion
    }
}