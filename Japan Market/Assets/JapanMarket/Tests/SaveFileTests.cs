using System.IO;
using System.Text.RegularExpressions;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JapanMarket.Tests
{
    /// <summary>
    /// O save indo e voltando de um ARQUIVO DE VERDADE.
    ///
    /// O `SaveFormatTests` prova que o JSON não perde campo; este prova o resto:
    /// que a gravação é atômica, que o backup existe, e que um arquivo
    /// corrompido não vira exceção na inicialização. São coisas diferentes, e a
    /// segunda só dá para verificar tocando em disco.
    ///
    /// Escreve numa pasta temporária, nunca no `persistentDataPath`: já houve
    /// neste projeto um teste que rodou com save automático ligado e gravou por
    /// cima da partida de alguém. Um teste não encosta no save do jogador.
    /// </summary>
    [TestFixture]
    public class SaveFileTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "jm-save-tests-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);

            _path = Path.Combine(_dir, "teste.save.json");
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* pasta temporária presa não é motivo para falhar o teste */ }
        }

        private static GameSave Save(int day, long balance) => new()
        {
            Version = GameSave.CurrentVersion,
            Clock = new ClockSave { Day = day },
            Ledger = new LedgerSave { BalanceYen = balance },
        };

        [Test]
        public void O_save_vai_e_volta_do_disco()
        {
            Assert.IsTrue(SaveFile.TryWriteTo(_path, Save(5, 4200)));
            Assert.IsTrue(File.Exists(_path));

            Assert.IsTrue(SaveFile.TryReadFrom(_path, out GameSave loaded, useBackup: false));
            Assert.AreEqual(5, loaded.Clock.Day);
            Assert.AreEqual(4200, loaded.Ledger.BalanceYen);
        }

        [Test]
        public void Gravar_por_cima_guarda_o_anterior_no_backup()
        {
            // É o caso que já aconteceu aqui: um save sobrescrito sem que
            // existisse cópia de nada.
            SaveFile.TryWriteTo(_path, Save(1, 1000));
            SaveFile.TryWriteTo(_path, Save(2, 2000));

            Assert.IsTrue(File.Exists(_path + SaveFile.BackupSuffix));

            SaveFile.TryReadFrom(_path, out GameSave atual, useBackup: false);
            Assert.AreEqual(2, atual.Clock.Day);

            SaveFile.TryReadFrom(_path + SaveFile.BackupSuffix, out GameSave anterior,
                                 useBackup: false);
            Assert.AreEqual(1, anterior.Clock.Day, "O backup é a gravação de antes.");
        }

        [Test]
        public void A_primeira_gravacao_nao_inventa_backup()
        {
            SaveFile.TryWriteTo(_path, Save(1, 1000));

            Assert.IsFalse(File.Exists(_path + SaveFile.BackupSuffix));
        }

        [Test]
        public void Um_save_corrompido_cai_para_o_backup()
        {
            SaveFile.TryWriteTo(_path, Save(1, 1000));
            SaveFile.TryWriteTo(_path, Save(2, 2000));

            File.WriteAllText(_path, "{ isto não é json válido");
            LogAssert.Expect(LogType.Error,
                new Regex(@"^\[Save\] Não deu para ler '.+': JSON parse error:"));

            Assert.IsTrue(SaveFile.TryReadFrom(_path, out GameSave loaded, useBackup: true));
            Assert.AreEqual(1, loaded.Clock.Day, "Voltou um dia, e não à estaca zero.");
        }

        [Test]
        public void Sem_backup_um_save_corrompido_falha_sem_lancar()
        {
            // Falhar tem que virar "começar de novo", nunca uma exceção na
            // inicialização com a tela preta.
            File.WriteAllText(_path, "{{{{");
            LogAssert.Expect(LogType.Error,
                new Regex(@"^\[Save\] Não deu para ler '.+': JSON parse error:"));

            GameSave loaded = null;
            Assert.DoesNotThrow(
                () => SaveFile.TryReadFrom(_path, out loaded, useBackup: true));

            Assert.IsNull(loaded);
        }

        [Test]
        public void Um_arquivo_vazio_nao_e_um_save()
        {
            File.WriteAllText(_path, string.Empty);

            Assert.IsFalse(SaveFile.TryReadFrom(_path, out GameSave loaded, useBackup: false));
            Assert.IsNull(loaded);
        }

        [Test]
        public void Um_JSON_valido_que_nao_e_save_e_recusado()
        {
            // Sem a checagem de versão, isto viraria uma partida zerada aplicada
            // por cima do jogo do jogador.
            File.WriteAllText(_path, @"{""outraCoisa"":123}");

            Assert.IsFalse(SaveFile.TryReadFrom(_path, out GameSave loaded, useBackup: false));
            Assert.IsNull(loaded);
        }

        [Test]
        public void Apagar_leva_o_backup_junto()
        {
            // Senão "apagar o save" deixaria uma cópia que o carregamento
            // recupera sozinho, e o jogador voltaria para a partida que ele
            // acabou de mandar apagar.
            SaveFile.TryWriteTo(_path, Save(1, 1000));
            SaveFile.TryWriteTo(_path, Save(2, 2000));

            Assert.IsTrue(SaveFile.TryDeleteAt(_path));

            Assert.IsFalse(File.Exists(_path));
            Assert.IsFalse(File.Exists(_path + SaveFile.BackupSuffix));
            Assert.IsFalse(SaveFile.TryReadFrom(_path, out _, useBackup: true));
        }

        [Test]
        public void Um_temporario_nao_fica_para_tras()
        {
            SaveFile.TryWriteTo(_path, Save(1, 1000));

            Assert.IsFalse(File.Exists(_path + ".tmp"),
                "O .tmp é a garantia de gravação atômica, não lixo para o jogador ver.");
        }
    }
}
