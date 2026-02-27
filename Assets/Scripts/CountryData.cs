using UnityEngine;

[System.Serializable]
public class CountryData
{
    public string nameLong;
    public string isoA3EH;
    public int isoN3;
    public int index;

    public static CountryData Oceans = new CountryData("Oceans", "", -1, -1);

    public CountryData(string name, string isoString, int isoInt, int idx)
    {
        nameLong = name;
        isoA3EH = isoString;
        isoN3 = isoInt;
        index = idx;
    }
}
