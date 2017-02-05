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
        private int _maxSongLengthInMunites = 5;

        public MelodyGenerationBot(string apiToken)
        {
            _api = new BotApi(apiToken);
            this.MessageRecieved += MelodyGenerationBot_MessageRecieved;
        }

        private void MelodyGenerationBot_MessageRecieved(Message message)
        {
            if (message?.Text == null)
                return;
            var result = _compiler.Parse(_compiler.Tokenize(message.Text.Trim(' ').ToLower()));
            if (result.Errors.Count == 0)
            {
                result.Song.Name = message.Text;
                if (result.Song.Length > TimeSpan.FromMinutes(_maxSongLengthInMunites).TotalMilliseconds)
                    _api.SendMessage(message.Chat, $"Ваша мелодия слишком длинная. Разрешена генерация мелодий не длиннее {_maxSongLengthInMunites} минут.").Wait();
                else
                    SendSong(message.Chat, result.Song);
            }
            else
            {
                _api.SendMessage(message.Chat, $"Не могу понять вашу мелодию.").Wait();
                _api.SendMessage(message.Chat, $"В коде мелодии были обнаружены ошибки, а именно:{Environment.NewLine}" +
                                      string.Join($";{Environment.NewLine}",
                                          result.Errors.Select(token => $"• на позиции {token.Index}: <i>{token.Value}</i>")) + ".").Wait();
            }
        }
        private void SendSong(Chat chat, Song song)
        {
            var generationTaskTask = Task.Run<byte[]>(() => GenerateMp3(song));
            do
            {
                _api.SendChatAction(chat, "upload_audio").Wait();
            } while (!generationTaskTask.Wait(4000));
            if (generationTaskTask.Result == null)
            {
                _api.SendMessage(chat, $"Во время генерации аудиофайла произошла ошибка.").Wait();
                return;
            }
            var stream = new MemoryStream(generationTaskTask.Result);
            var name = new string(song.Name.Take(100).ToArray());
            try
            {
                _api.SendChatAction(chat, "upload_audio").Wait();
                var sendingTask = Task.Run<Message>(
                    async () => await _api.SendAudio(chat, stream, name + ".mp3", (int)(song.Length / 1000), name));
                while (!sendingTask.Wait(4000))
                    _api.SendChatAction(chat, "upload_audio").Wait();
            }
            catch
            {
               _api.SendMessage(chat, $"Во время отправки аудиофайла произошла ошибка.").Wait();
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
                r.Dispose();
                wr.Dispose();
                return fileData;
            }
        }
    }
}