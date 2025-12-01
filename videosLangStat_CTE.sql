WITH TotalCount AS (
    -- 步驟 1: 只計算一次總數，並乘以 1.0 確保結果為浮點數
    SELECT COUNT(*) * 1.0 AS total_videos FROM videos
), 
LangStats AS (
    -- 步驟 2: 計算每個語言的數量和百分比
    SELECT 
        lang,
        COUNT(*) AS quantity,
        -- 使用總數 (total_videos) 作為分母計算百分比
        COUNT(*) * 1.0 / (SELECT total_videos FROM TotalCount) AS percentage
    FROM 
        videos
    GROUP BY 
        lang
)
-- 步驟 3: 語言分組結果
SELECT 
    lang, 
    quantity, 
    percentage 
FROM 
    LangStats

UNION ALL

-- 步驟 4: 加入總計行
SELECT 
    '合計' AS lang, 
    SUM(quantity) AS quantity, -- 直接對 LangStats 的 quantity 欄位求和得到總計
    1.0 AS percentage         -- 總計的百分比永遠是 1.0 (100%)
FROM 
    LangStats