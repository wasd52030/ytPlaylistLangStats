WITH TotalCount AS (
    SELECT COUNT(*) * 1.0 AS total_videos FROM videos
), 
LangStats AS (
    SELECT 
        lang,
        COUNT(*) AS quantity,
        COUNT(*) * 1.0 / (SELECT total_videos FROM TotalCount) AS percentage
    FROM 
        videos
    GROUP BY 
        lang
)
SELECT 
    lang, 
    quantity, 
    percentage 
FROM 
    LangStats

UNION ALL

SELECT 
    '合計' AS lang, 
    SUM(quantity) AS quantity,
    1.0 AS percentage
FROM 
    LangStats