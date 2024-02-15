namespace XFramework.External;

public class SeedString
{
    public string GenerateRandom(int length)
    {
        Random random = new Random();

        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}