namespace RoxyzsPortal;

// Japanese summaries correspond to the indexed release. A newer release uses the publisher's description.
internal static class KnownEntries
{
    internal static readonly Dictionary<string,(long Created, string Version, string Japanese)> Items = new()
    {
        ["GlamourSaver"] = (1787161242, "1.0.0.2", "調べたキャラクターのミラプリと装備情報をDiscordに送信します。"),
        ["CharacterArchive"] = (1787161326, "1.7.0.0", "キャラクター、ジョブ、プレイ時間、Lodestone情報をまとめて確認できます。"),
        ["AetherCurrentUnlocker"] = (1787161457, "1.0.0.1", "各拡張エリアの風脈を表示し、解放を支援します。"),
        ["AetherRadio"] = (1791029177, "1.2.0.0", "ゲーム内のBGMを選んで再生できます。"),
        ["RetainerRecall"] = (1791394018, "1.0.0.3", "リテイナーの出品アイテムの一括回収と、保存した単価での連続出品を補助します。"),
        ["AllaganLocalPlugin"] = (1791431943, "1.3.0.8", "所持品、所持ギル、マーケット履歴をローカルで確認できます。"),
        ["BaitBeacon"] = (1791697943, "1.0.0.3", "自動採集に必要な釣り餌を強調表示します。"),
        ["CommandAtlas"] = (1791954519, "1.1.0.1", "有効なプラグインのコマンドを検索し、実行・コピーできます。"),
        ["MarketLedger"] = (1792265421, "1.2.0.1", "マーケットやNPCからの購入履歴、支出、販売者、税額を記録します。"),
        ["KuzushiBeacon"] = (1792265489, "1.1.0.17", "PvPでの対象選択、崩し、生存支援と戦績記録をまとめます。"),
        ["PvPLedger"] = (1792265971, "1.0.0.1", "PvPの戦績を記録し、個人用オーバーレイで確認できます。"),
        ["MarketVoyager"] = (1792464927, "1.0.1.9", "購入リストに沿ってリムサからワールドを巡回します。"),
        ["GilBeacon"] = (1792465219, "1.0.0.4", "購入税を含めてもNPCへの売却で利益が出るマーケット出品を強調します。"),
        ["HotbarStudio"] = (1792840330, "1.0.0.1", "ホットバーをプレビューし、スロットやバーをコピー・移動できます。"),
        ["KeyboundPilot"] = (1793176998, "1.0.17.8", "キーバウンド・ブロウラーの自動入力と指定回数の周回を行います。"),
        ["ScreenshotSorter"] = (1793778502, "1.0.0.0", "通常UI、グループポーズ、UI非表示のスクリーンショットを分類します。"),
        ["VendorCompass"] = (1793991869, "1.0.0.0", "アイテムのメニューから販売NPCを探し、店舗への移動を支援します。"),
    };
}
