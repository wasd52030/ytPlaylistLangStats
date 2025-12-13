-- reference -> https://www.gss.com.tw/blog/sql-cte-recursive-query-postgresql-mssql
WITH PlaylistVideos AS (
    SELECT v.video_id, v.title, v.lang, pl.position
    FROM playlist_item pl
    JOIN videos v ON pl.video_id = v.video_id
    JOIN playlists p ON pl.playlist_id = p.playlist_id
    WHERE p.title = '酷東東'
),
UnnestedVideos AS (
    SELECT
        -- 1. 移除首尾的 '[' 和 ']'
        -- 2. 將 '][' 替換成逗號 ','
        -- 3. 使用 string_to_array 拆分成陣列
        -- 4. 使用 unnest 將陣列展開成多行
        unnest(string_to_array(TRIM(LEADING '[' FROM TRIM(TRAILING ']' FROM t1.lang)), '][')) AS lang_split
    FROM
        PlaylistVideos t1
),
TotalCount AS (
    SELECT COUNT(*) * 1.0 AS total_videos 
    FROM UnnestedVideos
),
LangStats AS (
    SELECT 
        lang_split AS lang,
        COUNT(*) AS quantity,
        COUNT(*) * 1.0 / (SELECT total_videos FROM TotalCount) AS percentage
    FROM UnnestedVideos
    GROUP BY lang
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