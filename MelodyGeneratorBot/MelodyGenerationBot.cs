using System;
using System.IO;
using System.Linq;
using Telegram;
using Simue;
using System.Threading.Tasks;
using NAudio.Lame;
using NAudio.Wave;
using WaveGenerator;

namespace MelodyGeneratorBot
{
    public class MelodyGenerationBot : Bot
    {
        private BotApi _api;
        SimueCompiler _compiler = new SimueCompiler();

        public MelodyGenerationBot(string apiToken)
        {
            _api = new BotApi(apiToken);
            this.MessageRecieved += MelodyGenerationBot_MessageRecieved;
        }

        private void MelodyGenerationBot_MessageRecieved(Message message)
        {
            var result = _compiler.Parse(_compiler.Tokenize(message.Text.Trim(' ').ToLower()));
            if (result.Errors.Count == 0)
            {
                var song = result.Song;
                song.Name = message.Text;
                int maxMin = 20;
                if (song.Length > TimeSpan.FromMinutes(maxMin).TotalMilliseconds)
                    _api.SendMessage(message.Chat, $"Ваша мелодия слишком длинная. Разрешена генерация мелодий не длиннее {maxMin} минут.").Wait();
                else
                    SendSong(_api, message.Chat, song).Wait();
            }
            else
            {
                _api.SendMessage(message.Chat, $"Не могу понять вашу мелодию.").Wait();
                _api.SendMessage(message.Chat, $"В коде мелодии были обнаружены ошибки, а именно:{Environment.NewLine}" +
                                      string.Join($";{Environment.NewLine}",
                                          result.Errors.Select(token => $"• на позиции {token.Index}: <i>{token.Value}</i>")) + ".").Wait();
            }

        }
        private async Task SendSong(BotApi botapi, Chat chat, Song song)
        {
            var generationTaskTask = Task.Run<byte[]>(() => GenerateMp3(song));
            do
            {
                await botapi.SendChatAction(chat, "upload_audio");
            } while (!generationTaskTask.Wait(4000));
            if (generationTaskTask.Result == null)
            {
                await botapi.SendMessage(chat, $"Во время генерации аудиофайла произошла ошибка.");
                return;
            }
            var stream = new MemoryStream(generationTaskTask.Result);
            var name = new string(song.Name.Take(100).ToArray());
            try
            {
                await botapi.SendChatAction(chat, "upload_audio");
                var sendingTask = Task.Run<Message>(
                    async () => await botapi.SendAudio(chat, stream, name + ".mp3", (int)(song.Length / 1000), name));
                while (!sendingTask.Wait(4000))
                    await botapi.SendChatAction(chat, "upload_audio");
            }
            catch
            {
                await botapi.SendMessage(chat, $"Во время отправки аудиофайла произошла ошибка.");
            }
        }
        private byte[] GenerateMp3(Song song)
        {
            if (song.Notes == null)
                return null;
            using (var generatedSongStream = new MemoryStream())
            {
                var mp3File = new MemoryStream();
                var waveFile = new WaveFile(22050, BitDepth.Bit16, 1, generatedSongStream);
                var sg = new SoundGenerator(waveFile);
                foreach (var note in song.Notes)
                    sg.AddSimpleTone(note.Frequency, note.Duration);
                sg.Save();
                generatedSongStream.Position = 0;
                var r = new WaveFileReader(generatedSongStream);
                var wr = new LameMP3FileWriter(mp3File, r.WaveFormat, 96);
                r.CopyTo(wr);
                wr.Flush();
                var fileData = mp3File.ToArray();
                mp3File.Dispose();
                return fileData;
            }
        }
    }
}