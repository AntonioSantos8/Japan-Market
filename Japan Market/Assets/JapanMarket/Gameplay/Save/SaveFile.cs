using System;
using System.IO;
using JapanMarket.Domain;
using UnityEngine;

namespace JapanMarket.Gameplay
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
    /// </summary>
    public static class SaveFile
    {
        public const string DefaultName = "japanmarket.save.json";

        /// <summary>
        /// <c>Application.persistentDataPath</c>: é o único lugar gravável em
        /// todas as plataformas. A pasta do jogo é somente leitura em build
        /// instalado, e escrever lá funciona no editor e falha no jogador.
        /// </summary>
        public static string PathFor(string fileName = DefaultName) =>
            Path.Combine(Application.persistentDataPath,
                         string.IsNullOrWhiteSpace(fileName) ? DefaultName : fileName);

        public static bool Exists(string fileName = DefaultName) =>
            File.Exists(PathFor(fileName));

        public static bool TryWrite(GameSave save, string fileName = DefaultName)
        {
            if (save == null) return false;

            string path = PathFor(fileName);
            string temp = path + ".tmp";

            try
            {
                string json = JsonUtility.ToJson(save, prettyPrint: true);

                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                File.WriteAllText(temp, json);

                // Replace preserva o antigo até a troca; Move falha se o destino
                // existe, e Delete+Move abre exatamente a janela que queremos
                // fechar.
                if (File.Exists(path)) File.Replace(temp, path, null);
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
        /// </summary>
        public static bool TryRead(out GameSave save, string fileName = DefaultName)
        {
            save = null;
            string path = PathFor(fileName);

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

        public static bool TryDelete(string fileName = DefaultName)
        {
            string path = PathFor(fileName);

            try
            {
                if (File.Exists(path)) File.Delete(path);
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
