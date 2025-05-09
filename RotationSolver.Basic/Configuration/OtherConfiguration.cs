﻿using ECommons.DalamudServices;

namespace RotationSolver.Basic.Configuration;

internal class OtherConfiguration
{
    public static HashSet<uint> HostileCastingArea = [];
    public static HashSet<uint> HostileCastingTank = [];
    public static HashSet<uint> HostileCastingKnockback = [];
    public static HashSet<uint> HostileCastingStop = [];

    public static SortedList<uint, float> AnimationLockTime = [];

    public static Dictionary<uint, string[]> NoHostileNames = [];
    public static Dictionary<uint, string[]> NoProvokeNames = [];
    public static Dictionary<uint, Vector3[]> BeneficialPositions = [];

    public static HashSet<uint> DangerousStatus = [];
    public static HashSet<uint> PriorityStatus = [];
    public static HashSet<uint> InvincibleStatus = [];
    public static HashSet<uint> NoCastingStatus = [];
    public static HashSet<uint> PrioTargetId = [];
    public static HashSet<uint> AutoStatusOrder = [];

    public static RotationSolverRecord RotationSolverRecord = new();

    public static void Init()
    {
        if (!Directory.Exists(Svc.PluginInterface.ConfigDirectory.FullName))
        {
            Directory.CreateDirectory(Svc.PluginInterface.ConfigDirectory.FullName);
        }

        Task.Run(() => InitOne(ref DangerousStatus, nameof(DangerousStatus)));
        Task.Run(() => InitOne(ref PriorityStatus, nameof(PriorityStatus)));
        Task.Run(() => InitOne(ref InvincibleStatus, nameof(InvincibleStatus)));
        Task.Run(() => InitOne(ref PrioTargetId, nameof(PrioTargetId)));
        Task.Run(() => InitOne(ref AutoStatusOrder, nameof(AutoStatusOrder)));
        Task.Run(() => InitOne(ref NoHostileNames, nameof(NoHostileNames)));
        Task.Run(() => InitOne(ref NoProvokeNames, nameof(NoProvokeNames)));
        Task.Run(() => InitOne(ref AnimationLockTime, nameof(AnimationLockTime)));
        Task.Run(() => InitOne(ref HostileCastingArea, nameof(HostileCastingArea)));
        Task.Run(() => InitOne(ref HostileCastingTank, nameof(HostileCastingTank)));
        Task.Run(() => InitOne(ref BeneficialPositions, nameof(BeneficialPositions)));
        Task.Run(() => InitOne(ref RotationSolverRecord, nameof(RotationSolverRecord), false));
        Task.Run(() => InitOne(ref NoCastingStatus, nameof(NoCastingStatus)));
        Task.Run(() => InitOne(ref HostileCastingKnockback, nameof(HostileCastingKnockback)));
        Task.Run(() => InitOne(ref HostileCastingStop, nameof(HostileCastingStop)));
    }

    public static Task Save()
    {
        return Task.Run(async () =>
        {
            await SavePriorityStatus();
            await SaveDangerousStatus();
            await SaveInvincibleStatus();
            await SavePrioTargetId();
            await SaveAutoStatusOrder();
            await SaveNoHostileNames();
            await SaveAnimationLockTime();
            await SaveHostileCastingArea();
            await SaveHostileCastingTank();
            await SaveBeneficialPositions();
            await SaveRotationSolverRecord();
            await SaveNoProvokeNames();
            await SaveNoCastingStatus();
            await SaveHostileCastingKnockback();
            await SaveHostileCastingStop();
        });
    }
    #region Action Tab
    public static void ResetHostileCastingArea()
    {
        InitOne(ref HostileCastingArea, nameof(HostileCastingArea), true, true);
        SaveHostileCastingArea().Wait();
    }

    public static void ResetHostileCastingTank()
    {
        InitOne(ref HostileCastingTank, nameof(HostileCastingTank), true, true);
        SaveHostileCastingTank().Wait();
    }

    public static void ResetHostileCastingKnockback()
    {
        InitOne(ref HostileCastingKnockback, nameof(HostileCastingKnockback), true, true);
        SaveHostileCastingKnockback().Wait();
    }

    public static void ResetHostileCastingStop()
    {
        InitOne(ref HostileCastingStop, nameof(HostileCastingStop), true, true);
        SaveHostileCastingStop().Wait();
    }

    public static Task SaveHostileCastingArea()
    {
        return Task.Run(() => Save(HostileCastingArea, nameof(HostileCastingArea)));
    }

    public static Task SaveHostileCastingTank()
    {
        return Task.Run(() => Save(HostileCastingTank, nameof(HostileCastingTank)));
    }

    private static Task SaveHostileCastingKnockback()
    {
        return Task.Run(() => Save(HostileCastingKnockback, nameof(HostileCastingKnockback)));
    }

    private static Task SaveHostileCastingStop()
    {
        return Task.Run(() => Save(HostileCastingStop, nameof(HostileCastingStop)));
    }
    #endregion

    #region Status Tab

    public static void ResetPriorityStatus()
    {
        InitOne(ref PriorityStatus, nameof(PriorityStatus), true, true);
        SavePriorityStatus().Wait();
    }

    public static void ResetInvincibleStatus()
    {
        InitOne(ref InvincibleStatus, nameof(InvincibleStatus), true, true);
        SaveInvincibleStatus().Wait();
    }

    public static void ResetDangerousStatus()
    {
        InitOne(ref DangerousStatus, nameof(DangerousStatus), true, true);
        SaveDangerousStatus().Wait();
    }

    public static void ResetNoCastingStatus()
    {
        InitOne(ref NoCastingStatus, nameof(NoCastingStatus), true, true);
        SaveNoCastingStatus().Wait();
    }

    public static Task SavePriorityStatus()
    {
        return Task.Run(() => Save(PriorityStatus, nameof(PriorityStatus)));
    }

    public static Task SaveInvincibleStatus()
    {
        return Task.Run(() => Save(InvincibleStatus, nameof(InvincibleStatus)));
    }

    public static Task SaveDangerousStatus()
    {
        return Task.Run(() => Save(DangerousStatus, nameof(DangerousStatus)));
    }

    public static Task SaveNoCastingStatus()
    {
        return Task.Run(() => Save(NoCastingStatus, nameof(NoCastingStatus)));
    }

    #endregion
    public static Task SaveRotationSolverRecord()
    {
        return Task.Run(() => Save(RotationSolverRecord, nameof(RotationSolverRecord)));
    }
    public static Task SaveNoProvokeNames()
    {
        return Task.Run(() => Save(NoProvokeNames, nameof(NoProvokeNames)));
    }

    public static Task SaveBeneficialPositions()
    {
        return Task.Run(() => Save(BeneficialPositions, nameof(BeneficialPositions)));
    }

    public static void ResetPrioTargetId()
    {
        InitOne(ref PrioTargetId, nameof(PrioTargetId), true, true);
        SavePrioTargetId().Wait();
    }

    public static Task SavePrioTargetId()
    {
        return Task.Run(() => Save(PrioTargetId, nameof(PrioTargetId)));
    }

    public static void ResetAutoStatusOrder()
    {
        InitOne(ref AutoStatusOrder, nameof(AutoStatusOrder), true, true);
        SaveAutoStatusOrder().Wait();
    }

    public static Task SaveAutoStatusOrder()
    {
        return Task.Run(() => Save(AutoStatusOrder, nameof(AutoStatusOrder)));
    }

    public static Task SaveNoHostileNames()
    {
        return Task.Run(() => Save(NoHostileNames, nameof(NoHostileNames)));
    }

    public static Task SaveAnimationLockTime()
    {
        return Task.Run(() => Save(AnimationLockTime, nameof(AnimationLockTime)));
    }

    private static string GetFilePath(string name)
    {
        var directory = Svc.PluginInterface.ConfigDirectory.FullName;

        return directory + $"\\{name}.json";
    }

    private static void Save<T>(T value, string name)
        => SavePath(value, GetFilePath(name));

    private static void SavePath<T>(T value, string path)
    {
        int retryCount = 3;
        int delay = 1000; // 1 second delay

        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                File.WriteAllText(path,
                JsonConvert.SerializeObject(value, Formatting.Indented, new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.None,
                }));
                return; // Exit the method if successful
            }
            catch (IOException ex) when (i < retryCount - 1)
            {
                Svc.Log.Warning(ex, $"Failed to save the file to {path}. Retrying in {delay}ms...");
                Thread.Sleep(delay); // Wait before retrying
            }
            catch (Exception ex)
            {
                Svc.Log.Warning(ex, $"Failed to save the file to {path}");
                return; // Exit the method if an unexpected exception occurs
            }
        }
    }

    private static void InitOne<T>(ref T value, string name, bool download = true, bool forceDownload = false) where T : new()
    {
        var path = GetFilePath(name);
        Svc.Log.Info($"Initializing {name} from {path}");

        if (File.Exists(path) && !forceDownload)
        {
            try
            {
                value = JsonConvert.DeserializeObject<T>(File.ReadAllText(path))!;
                if (value == null) throw new Exception("Deserialized value is null.");
                Svc.Log.Info($"Loaded {name} from local file.");
            }
            catch (Exception ex)
            {
                Svc.Log.Warning(ex, $"Failed to load {name} from local file. Reinitializing to default.");
                value = new T(); // Reinitialize to default
            }
        }
        else if (download || forceDownload)
        {
            try
            {
                using var client = new HttpClient();
                var str = client.GetStringAsync($"https://raw.githubusercontent.com/{Service.USERNAME}/{Service.REPO}/main/Resources/{name}.json").Result;

                File.WriteAllText(path, str);
                value = JsonConvert.DeserializeObject<T>(str, new JsonSerializerSettings()
                {
                    MissingMemberHandling = MissingMemberHandling.Error,
                    Error = delegate (object? sender, Newtonsoft.Json.Serialization.ErrorEventArgs args) // Allow sender to be null
                    {
                        args.ErrorContext.Handled = true;
                    }
                })!;
                if (value == null) throw new Exception("Deserialized value is null.");
                Svc.Log.Info($"Downloaded and loaded {name} from GitHub.");
            }
            catch (Exception ex)
            {
                Svc.Log.Warning(ex, $"Failed to download {name} from GitHub. Reinitializing to default.");
                value = new T(); // Reinitialize to default
                SavePath(value, path); // Save the default value
            }
        }
        else
        {
            value = new T(); // Reinitialize to default
            SavePath(value, path); // Save the default value
        }
    }
}
