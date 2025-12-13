-- reference -> https://www.gss.com.tw/blog/sql-cte-recursive-query-postgresql-mssql
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
),
Presentation as (
	SELECT 
	    lang,
	    quantity,
	    percentage,
	    0 AS sort_order
	FROM LangStats
	UNION ALL
	SELECT
	    '合計' AS lang,
	    SUM(quantity) AS quantity,
	    1.0 AS percentage,
	    1 AS sort_order
	FROM LangStats
)
SELECT lang, quantity, percentage from Presentation
ORDER BY sort_order ASC, quantity DESC;