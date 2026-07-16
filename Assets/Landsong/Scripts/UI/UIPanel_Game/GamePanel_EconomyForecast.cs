using System.Text;
using Landsong;
using Landsong.InventorySystem;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GamePanel_EconomyForecast : MonoBehaviour
{
    [SerializeField] private TMP_Text itemForecastText;
    [SerializeField] private TMP_Text residentialForecastText;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private bool refreshOnEnable = true;

    private void OnEnable()
    {
        if (refreshOnEnable)
        {
            RefreshForecast();
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        RefreshForecast();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void RefreshForecast()
    {
        var forecast = GameSystem.Instance?.Services.EconomyForecast?.ForecastNextTurn();
        if (forecast == null)
        {
            SetText(itemForecastText, "经济预测服务未初始化。");
            SetText(residentialForecastText, string.Empty);
            SetText(warningText, string.Empty);
            return;
        }

        RenderItems(forecast);
        RenderResidential(forecast);
        RenderWarnings(forecast);
    }

    private void RenderItems(EconomyForecastResult forecast)
    {
        if (itemForecastText == null)
        {
            return;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < forecast.ItemLines.Count; i++)
        {
            var line = forecast.ItemLines[i];
            builder.Append(line.ItemId)
                .Append("  当前 ")
                .Append(line.CurrentQuantity)
                .Append("  -消耗 ")
                .Append(line.ExpectedConsumption)
                .Append("  +产出 ")
                .Append(line.ExpectedProduction)
                .Append("  -损耗 ")
                .Append(line.ExpectedLoss)
                .Append("  = ")
                .Append(line.ProjectedQuantity);

            if (line.Shortage > 0)
            {
                builder.Append("  缺口 ").Append(line.Shortage);
            }

            builder.AppendLine();
        }

        itemForecastText.text = builder.Length == 0 ? "下一回合没有可预测的物品变化。" : builder.ToString();
    }

    private void RenderResidential(EconomyForecastResult forecast)
    {
        if (residentialForecastText == null)
        {
            return;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < forecast.ResidentialForecasts.Count; i++)
        {
            var row = forecast.ResidentialForecasts[i];
            builder.Append(row.BuildingDisplayName)
                .Append("  人口 ")
                .Append(row.Population)
                .Append("  食物 ")
                .Append(row.FoodSatisfied ? "满足" : "不足")
                .Append("  预计种类 ")
                .Append(row.PredictedDietVariety)
                .Append("  饮食分 ")
                .Append(row.PredictedDietScore.ToString("0.#"))
                .Append("  当前生活质量 ")
                .Append(row.CurrentLifeQuality.ToString("0.#"))
                .AppendLine();
        }

        residentialForecastText.text = builder.Length == 0 ? "当前没有居民饮食预测。" : builder.ToString();
    }

    private void RenderWarnings(EconomyForecastResult forecast)
    {
        if (warningText == null)
        {
            return;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < forecast.Warnings.Count; i++)
        {
            builder.Append("• ").AppendLine(forecast.Warnings[i]);
        }

        warningText.text = builder.ToString();
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null)
        {
            label.text = value ?? string.Empty;
        }
    }
}
