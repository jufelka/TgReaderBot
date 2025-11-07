DotNetEnv.Env.Load();
DotNetEnv.Env.TraversePath().Load();

string tgToken = Environment.GetEnvironmentVariable("TG_BOT_TOKEN");

await new Db().CreateDb();
await new Tg(tgToken).Start();
