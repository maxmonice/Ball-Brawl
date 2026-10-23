namespace BallBrawl;

static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--selftest")) { MessageBox.Show(GameEngine.SelfTest(), "Ball Brawl self-test"); return; }   // dotnet run -- --selftest
        Application.Run(new GameForm());
    }
}
