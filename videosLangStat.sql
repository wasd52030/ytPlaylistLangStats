-- reference -> https://stackoverflow.com/a/43855496/15407937
-- The result of COUNT() is an integer.
-- To calculate the percentage, it is necessary to cast c to a float by multiplying it by 1.0.

SELECT lang,quantity,percentage FROM (
	SELECT lang,COUNT(*) as quantity,COUNT(*) * 1.0 / (SELECT COUNT(*) FROM videos) AS percentage
	FROM videos GROUP BY lang ORDER BY quantity DESC,lang ASC
)
union all
SELECT '合計' as lang, COUNT(*) as quantity, COUNT(*) * 1.0 / (SELECT COUNT(*) FROM videos) as percentage from videos;