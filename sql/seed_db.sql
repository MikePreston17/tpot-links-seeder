drop table if exists railway.TPOTPapers;

CREATE TABLE railway.TPOTPapers (
    id int,
    wordpress_id int,
    author varchar(255),
    category varchar(255),
    link varchar(255),
    excerpt TEXT,
    markdown TEXT,
    frontmatter TEXT,
    RawJson LONGTEXT
);

insert into railway.TPOTPapers 
	values (1, 2, 10, "blah", "blah",  "blah",  "blah",  "blah", "blah")
	 ,(1, 2, 10, "blah", "blah",  "blah",  "blah",  "blah", "blah")
	 ,(1, 2, 10, "blah", "blah",  "blah",  "blah",  "blah", "blah")
	 ,(1, 2, 10, "blah", "blah",  "blah",  "blah",  "blah", "blah")
	 , (1, 2, 10, "blah", "blah",  "blah",  "blah",  "blah", "blah")
  ;

select * from railway.TPOTPapers;