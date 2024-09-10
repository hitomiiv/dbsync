# sqlorder-cli
## Keeping database objects in version control can be frustrating and sometimes expensive depending on the tooling. Let's fix that.

sqlorder-cli provides a dead-simple way to order and combine SQL scripts for your database or other cli tools. It analyzes references to other input files to determine the script order, so you can keep one file per database object. This way you can easily track changes to SQL scripts in version control without making them a nightmare to deploy.  

If you would still prefer to use migration scripts (i.e. 015_my_script.sql), don't panic. It will simply order these alphabetically as you'd expect. 

sqlorder-cli aims to be a simple, non-intrusive addition to your workflow and will not provide the same level of robustness that other migration tools can provide.

## Limitations
- Filenames for each non-migration script must exactly match the object it creates or alters
- Script changes are *only* tracked via version control or your folder structure. It has no other way of knowing which scripts have already been run for a database. This results in some consequences that you might not find in other migration tools:
  1. Each script must be idempotent, or in other words, running it twice should not wreck havoc. Make sure to use patterns for "create or alter" and "add column if it doesn't exist" etc.
  2. If you need to modify a schema after the inital "create" command, it still needs to be done in a separate command. This can be in either the same file or it's own migration.
  3. Dependencies can only be resolved from the file inputs of the command. If you run the same command twice on different directories, it will not be able to analyze the script dependencies between the two.

## Usage
| Command | Description |
| ------- | ----------- |
| sqlorder \[--concat\] | Prints the ordered scripts as filenames from stdin. If "--concat" is provided, the contents of each script will be printed instead | 

## Examples
Print the order of all scripts in a directory:
```bash
ls /path/to/directory | sqlorder
```

Combine all scripts in the current directory into a single file:
```bash
ls | sqlorder --concat > all.sql
```

Create a patch file for a database from the last commit:
```bash
git diff-tree -r --name-only HEAD | sqlorder --concat > patch.sql
```
Replace "HEAD" with any other commit id. The program will automatically filter out any non-sql files.

The outputs of these commands can and should be piped into other cli tools to interface with your preferred DBMS, for example sqlcmd, psql, etc.

## Algorithm
**Given a directory of sql scripts like this:**
- Scripts
  - 01_some_migration.sql
  - 02_another_migration.sql
  - proc1.sql (references proc2)
  - proc2.sql 
  - table1.sql
  - table2.sql

**The program will output the scripts in the correct order with the following rules:**
1. Schema scripts come first, then migrations, then functions and procedures
2. Migration scripts are ordered alphabetically. These are any scripts that start with a number
3. Non-migration scripts are ordered by their dependencies, i.e. if proc2 relies on proc1, proc1 will come first
4. The filenames of each non-migration script (tables, functions, etc.) must exactly match the database object they create. For example, if a script creates a procedure named `spMyProcedure`, it must be named `spMyProcedure.sql`

**In the above example, the output would be the following:**
```
table1.sql
table2.sql
01_some_migration.sql
02_another_migration.sql
proc2.sql
proc1.sql
```

## License
This project is under the MIT license. Feel free to use it for whatever you want.
