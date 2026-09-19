using System;
using System.IO;
using UnityEngine;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Lê e escreve o save em disco. Só isso — o que vai dentro é problema do
    /// <see cref="SaveService"/>.
    ///
    /// A escrita é feita em DOIS passos: grava num arquivo temporário e só então
    /// substitui o definitivo. Não é paranoia — o jogador vai salvar no
    /// fechamento do dia, e um Alt+F4 ou uma queda de energia no meio de um
    /// <c>File.WriteAllText</c> deixa o save truncado. Com a troca no fim, o
    /// arquivo antigo continua inteiro até o novo estar completo em disco: o
    /// pior caso passa a ser perder a última sessão, em vez de perder tudo.
    ///
    /// E a troca guarda o anterior num <c>.bak</c>. Isso custa um arquivo e
    /// resolve o caso que já aconteceu neste projeto: um teste rodou com save
    /// automático ligado e gravou por cima de uma partida, sem que existisse
    /// cópia de nada. Com o backup, um save sobrescrito por engano — ou
    /// corrompido — deixa de ser perda definitiva.
    ///
    /// Mora em Domain, e não em Gameplay, por um motivo prático: não há aqui
    /// MonoBehaviour, cena nem GameObject — só arquivo e JSON. Em Gameplay ele
    /// era intestável, porque o assembly de testes não referencia Gameplay de
    /// propósito. As sobrecargas que recebem o caminho inteiro existem para
    /// isso: o teste escreve numa pasta temporária, sem estado global e sem
    /// encostar no save do jogador.
    /// </summary>
    public static class SaveFile
    {
        public const string DefaultName = "japanmarket.save.json";

        /// <summary>Sufixo da cópia anterior. Fica ao lado do arquivo principal.</summary>
        public const string BackupSuffix = ".bak";

        /// <summary>
        /// <c>Application.persistentDataPath</c>: é o único lugar gravável em
        /// todas as plataformas. A pasta do jogo é somente leitura em build
        /// instalado, e escrever lá funciona no editor e falha no jogador.
        /// </summary>
        public static string PathFor(string fileName = DefaultName) =>
            Path.Combine(Application.persistentDataPath,
                         string.IsNullOrWhiteSpace(fileName) ? DefaultName : fileName);

        public static string BackupPathFor(string fileName = DefaultName) =>
            PathFor(fileName) + BackupSuffix;

        public static bool Exists(string fileName = DefaultName) =>
            File.Exists(PathFor(fileName));

        public static bool BackupExists(string fileName = DefaultName) =>
            File.Exists(BackupPathFor(fileName));

        public static bool TryWrite(GameSave save, string fileName = DefaultName) =>
            TryWriteTo(PathFor(fileName), save);

        /// <summary>Mesma gravação, num caminho absoluto qualquer.</summary>
        public static bool TryWriteTo(string path, GameSave save)
        {
            if (save == null || string.IsNullOrWhiteSpace(path)) return false;

            string temp = path + ".tmp";

            try
            {
                string json = JsonUtility.ToJson(save, prettyPrint: true);

                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                File.WriteAllText(temp, json);

                // Replace preserva o antigo até a troca; Move falha se o destino
                // existe, e Delete+Move abre exatamente a janela que queremos
                // fechar.
                //
                // O terceiro argumento é o backup, e o próprio Replace o escreve
                // na MESMA operação: não existe instante em que o antigo já foi
                // embora e o novo ainda não chegou.
                if (File.Exists(path)) File.Replace(temp, path, path + BackupSuffix);
                else File.Move(temp, path);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Não deu para gravar em '{path}': {e.Message}");

                TryDeleteTemp(temp);
                return false;
            }
        }

        /// <summary>
        /// Lê o save. Devolve false — e NÃO lança — para arquivo inexistente,
        /// vazio ou corrompido: um save quebrado tem que virar "começar de
        /// novo", não uma exceção na inicialização com a tela preta.
        ///
        /// Corrompido, tenta o <c>.bak</c> antes de desistir. Voltar um dia é
        /// muito melhor que voltar à estaca zero, e o jogador é avisado de que
        /// isso aconteceu — carregar o backup em silêncio faria ele perder um
        /// dia de progresso sem entender por quê.
        /// </summary>
        public static bool TryRead(out GameSave save, string fileName = DefaultName) =>
            TryReadFrom(PathFor(fileName), out save, useBackup: true);

        /// <summary>Mesma leitura, de um caminho absoluto qualquer.</summary>
        public static bool TryReadFrom(string path, out GameSave save, bool useBackup)
        {
            if (ReadOne(path, out save)) return true;
            if (!useBackup) return false;

            string backup = path + BackupSuffix;
            if (!File.Exists(backup)) return false;

            if (!ReadOne(backup, out save))
            {
                Debug.LogError("[Save] O save e a cópia de segurança estão ilegíveis.");
                return false;
            }

            Debug.LogWarning(
                "[Save] O arquivo principal não pôde ser lido. Carregando a cópia de " +
                "segurança — o progresso da última sessão salva pode não estar aqui.");

            return true;
        }

        private static bool ReadOne(string path, out GameSave save)
        {
            save = null;

            try
            {
                if (!File.Exists(path)) return false;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return false;

                save = JsonUtility.FromJson<GameSave>(json);

                // JsonUtility devolve null em JSON inválido, e um objeto com
                // tudo zerado em JSON válido que não é um save. A versão zero
                // pega o segundo caso.
                if (save == null || save.Version <= 0)
                {
                    Debug.LogWarning($"[Save] '{path}' não parece um save deste jogo.");
                    save = null;
                    return false;
                }

                if (save.Version > GameSave.CurrentVersion)
                {
                    Debug.LogWarning(
                        $"[Save] O arquivo é da versão {save.Version} e este jogo entende " +
                        $"até a {GameSave.CurrentVersion}. Carregando assim mesmo — o que " +
                        "for novo demais será ignorado.");
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Não deu para ler '{path}': {e.Message}");

                save = null;
                return false;
            }
        }

        public static bool TryDelete(string fileName = DefaultName) =>
            TryDeleteAt(PathFor(fileName));

        /// <summary>Apaga o save e o backup num caminho absoluto qualquer.</summary>
        public static bool TryDeleteAt(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);

                // O backup vai junto. Sem isto, "apagar o save" deixaria uma
                // cópia que o TryRead recupera sozinho no próximo carregamento —
                // e o jogador que pediu para recomeçar voltaria para a partida
                // que ele acabou de mandar apagar.
                string backup = path + BackupSuffix;
                if (File.Exists(backup)) File.Delete(backup);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Não deu para apagar '{path}': {e.Message}");
                return false;
            }
        }

        private static void TryDeleteTemp(string temp)
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { /* o temporário ficar para trás não quebra nada */ }
        }
    }
}
