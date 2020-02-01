﻿using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Base;
using static SysBot.Base.SwitchCommand;
using static SysBot.Base.SwitchButton;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Bot that launches Surprise Trade and repeatedly trades the same PKM. Dumps all received pkm to a dump folder.
    /// </summary>
    public class SurpriseTradeBot : PokeRoutineExecutor
    {
        public readonly PokemonPool<PK8> Pool = new PokemonPool<PK8>();
        private const int MyGiftAddress = 0x4293D8B0;
        private const int ReadPartyFormatPokeSize = 0x158;
        private const int TrainerDataOffset = 0x42935e48;
        private const int TrainerDataLength = 0x110;

        public string? DumpFolder { get; set; }

        public SurpriseTradeBot(string ip, int port) : base(ip, port) { }
        public SurpriseTradeBot(SwitchBotConfig cfg) : this(cfg.IP, cfg.Port) { }

        public void Load(PK8 pk) => Pool.Add(pk);
        public bool LoadFolder(string folder) => Pool.LoadFolder(folder);
        private PK8 GetInjectPokemonData() => Pool.GetRandomPoke();
        
        protected override async Task MainLoop(CancellationToken token)
        {
            // Initialize bot information
            var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
            Connection.Name = $"{sav.OT}-{sav.DisplayTID}";

            while (!token.IsCancellationRequested)
            {
                // Inject to b1s1
                Connection.Log("Starting next trade. Getting data...");
                var pkm = GetInjectPokemonData();
                await Connection.WriteBytesAsync(pkm.EncryptedPartyData, MyGiftAddress, token).ConfigureAwait(false);

                Connection.Log("Open Y-COM Menu");
                await Click(Y, 1_000, token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    break;

                Connection.Log("Select Surprise Trade");
                await Click(DDOWN, 0_100, token).ConfigureAwait(false);
                await Click(A, 4_000, token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    break;

                Connection.Log("Select Pokemon");
                // Box 1 Slot 1
                await Click(A, 0_700, token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    break;

                Connection.Log("Confirming...");
                await Click(A, 8_000, token).ConfigureAwait(false);
                for (int i = 0; i < 3; i++)
                    await Click(A, 0_700, token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    break;

                // Time we wait for a trade
                await Task.Delay(45_000, token).ConfigureAwait(false);
                await Click(Y, 0_700, token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    break;

                await WaitForTradeToFinish(token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    break;

                Connection.Log("Trade complete!");
                await ReadDumpB1S1(token).ConfigureAwait(false);
            }
        }

        private async Task<SAV8SWSH> GetFakeTrainerSAV(CancellationToken token)
        {
            var sav = new SAV8SWSH();
            var info = sav.MyStatus;
            var read = await Connection.ReadBytesAsync(TrainerDataOffset, TrainerDataLength, token).ConfigureAwait(false);
            read.CopyTo(info.Data);
            return sav;
        }

        private async Task Recover(CancellationToken token)
        {
            for (int i = 0; i < 3; i++)
                await Click(B, 1000, token).ConfigureAwait(false);
        }

        private async Task ReadDumpB1S1(CancellationToken token)
        {
            if (DumpFolder == null)
                return;

            // get pokemon from box1slot1
            var data = await Connection.ReadBytesAsync(MyGiftAddress, ReadPartyFormatPokeSize, token).ConfigureAwait(false);
            var pk8 = new PK8(data);
            File.WriteAllBytes(Path.Combine(DumpFolder, Util.CleanFileName(pk8.FileName)), pk8.DecryptedPartyData);
        }

        private static async Task WaitForTradeToFinish(CancellationToken token)
        {
            // probably needs to be longer for trade evolutions
            await Task.Delay(30_000, token).ConfigureAwait(false);
        }
    }
}