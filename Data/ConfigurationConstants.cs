namespace Sumtotal.ConfigurationsAutomation.Data
{
    public static class ConfigurationConstants
    {
        public const string PHASEI_QueryTemplate = @"
                    IF OBJECT_ID('tempdb.dbo.##Temp1', 'U') IS NOT NULL
                      DROP TABLE ##Temp1; 
                    IF OBJECT_ID('tempdb.dbo.##Temp2', 'U') IS NOT NULL
                      DROP TABLE ##Temp2; 
                    IF OBJECT_ID('tempdb.dbo.##temp_duplicate', 'U') IS NOT NULL
                      DROP TABLE ##temp_duplicate; 
                    Declare @delimeter varchar(10)='#'

                    ;With OrgHierarchy As 
                    ( 
                    Select O.OrganizationPK,O.Name, O.ParentOrganizationFK, CAST(O.Name as nvarchar(4000)) as Depth
                    From Organization O
                    Where O.OrganizationPK IN (select OrganizationPK from Organization where OrgDomainInd = 1 and isnull(ParentOrganizationFK,'') = '')
                    Union All 
                    Select O2.OrganizationPK,O2.Name, O2.ParentOrganizationFK, Depth+' --> ' + O2.Name
                    From Organization O2
                        Join OrgHierarchy 
                            On OrgHierarchy.OrganizationPK = O2.ParentOrganizationFK 
                    ) 
                    select ROW_NUMBER() OVER(PARTITION BY OrganizationPK  ORDER BY OrganizationPK ASC) row  ,
                    replace(p.Section,'Domain/'+Cast(o.OrganizationPK as nvarchar(9))+'/','') as CSec, replace(Data,char(13)+char(10),@delimeter) as Data1, *  
                    into  ##Temp1  
                    from Persist p
                    inner join OrgHierarchy O on p.Section like 'Domain/'+Cast(o.OrganizationPK as nvarchar(9))+'/%'
                    where 1=1 and (
                    ##SectionLookups##
                    )										
										 
                     ;WITH tmp([Name],OrganizationPK,section, DataItem, Data1) AS
                    (
                        SELECT
		                    Name,OrganizationPK,
                            CSec,
                            LEFT(Data1, CHARINDEX(@delimeter, Data1 + @delimeter) - 1),
                            STUFF(Data1, 1, CHARINDEX(@delimeter, Data1 + @delimeter), '')
                        FROM ##Temp1 where Data1 <> 'InheritedBaseDomain=-1'
                        UNION all

                        SELECT
	                    Name,OrganizationPK,
                            section,
                            LEFT(Data1, CHARINDEX(@delimeter, Data1 + @delimeter) - 1),
                            STUFF(Data1, 1, CHARINDEX(@delimeter, Data1 + @delimeter), '')
                        FROM tmp
                        WHERE
                            Data1 <> 'InheritedBaseDomain=-1'
                    )

                    SELECT 
                     Name,OrganizationPK,        section, DataItem ,  PARSENAME(REPLACE(Replace(DataItem,'.','<<>>'),'=','.'),2) as SettingKey,PARSENAME(REPLACE(Replace(DataItem,'.','<<>>'),'=','.'),1) as SettingValue 
                    into ##temp2
                    FROM tmp where DataItem <> 'InheritedBaseDomain=-1' and DataItem <> ''
                    ORDER BY Name,section
                    OPTION (MAXRECURSION 0);
                    update ##temp2 set SettingKey = REPLACE(DataItem,'=','') where SettingKey is null;
                    update ##temp2 set SettingValue = REPLACE(SettingValue,'<<>>','.') ;

                    Select  Distinct SettingKey
					into ##temp_duplicate from ##temp2
					where SettingKey<>'InheritedBaseDomain'
					 group by Organizationpk,SettingKey
					having count(*) >1

                    Update t2 SET t2.SettingKey=t2.section+'_'+td.SettingKey 
                    from ##temp_duplicate td 
					inner join ##temp2 t2 on t2.SettingKey=td.SettingKey;    

                    Select * from ##Temp1
                    Select * from ##temp2";

        public const string PHASEI_QueryTemplateForGlobalOnly = @"
                    IF OBJECT_ID('tempdb.dbo.##Temp1', 'U') IS NOT NULL
                      DROP TABLE ##Temp1; 
                    IF OBJECT_ID('tempdb.dbo.##Temp2', 'U') IS NOT NULL
                      DROP TABLE ##Temp2; 
                    IF OBJECT_ID('tempdb.dbo.##temp_duplicate', 'U') IS NOT NULL
                      DROP TABLE ##temp_duplicate; 
                    Declare @delimeter varchar(10)='#'

                    ;With OrgHierarchy As 
                    ( 
                    Select O.OrganizationPK,O.Name, O.ParentOrganizationFK, CAST(O.Name as nvarchar(4000)) as Depth
                    From Organization O
                    Where O.OrganizationPK IN (select OrganizationPK from Organization where OrgDomainInd = 1 and isnull(ParentOrganizationFK,'') = '')
                    Union All 
                    Select O2.OrganizationPK,O2.Name, O2.ParentOrganizationFK, Depth+' --> ' + O2.Name
                    From Organization O2
                        Join OrgHierarchy 
                            On OrgHierarchy.OrganizationPK = O2.ParentOrganizationFK 
                    ) 
                    select ROW_NUMBER() OVER(PARTITION BY OrganizationPK  ORDER BY OrganizationPK ASC) row  ,
                    replace(p.Section,'Domain/'+Cast(o.OrganizationPK as nvarchar(9))+'/','') as CSec, replace(Data,char(13)+char(10),@delimeter) as Data1, *  
                    into  ##Temp1  
                    from Persist p
                    inner join OrgHierarchy O on O.OrganizationPK = -1
                    where 1=1 and (
                    ##SectionLookups##
                    )										
										 
                    ;WITH tmp([Name],OrganizationPK,section, DataItem, Data1) AS
                    (
                        SELECT
		                    Name,OrganizationPK,
                            CSec,
                            LEFT(Data1, CHARINDEX(@delimeter, Data1 + @delimeter) - 1),
                            STUFF(Data1, 1, CHARINDEX(@delimeter, Data1 + @delimeter), '')
                        FROM ##Temp1 where Data1 <> 'InheritedBaseDomain=-1'
                        UNION all

                        SELECT
	                    Name,OrganizationPK,
                            section,
                            LEFT(Data1, CHARINDEX(@delimeter, Data1 + @delimeter) - 1),
                            STUFF(Data1, 1, CHARINDEX(@delimeter, Data1 + @delimeter), '')
                        FROM tmp
                        WHERE
                            Data1 <> 'InheritedBaseDomain=-1'
                    )

                    SELECT 
                    Name,OrganizationPK,        section, DataItem ,  PARSENAME(REPLACE(DataItem,'=','.'),2) as SettingKey,PARSENAME(REPLACE(DataItem,'=','.'),1) as SettingValue 
                    into ##temp2
                    FROM tmp where DataItem <> 'InheritedBaseDomain=-1' and DataItem <> ''
                    ORDER BY Name,section
					OPTION (MAXRECURSION 0);
                    update ##temp2 set SettingKey = REPLACE(DataItem,'=','') where SettingKey is null;
                    

                    Select  Distinct SettingKey
					into ##temp_duplicate from ##temp2
					where SettingKey<>'InheritedBaseDomain'
					 group by Organizationpk,SettingKey
					having count(*) >1

                    Update t2 SET t2.SettingKey=t2.section+'_'+td.SettingKey 
                    from ##temp_duplicate td 
					inner join ##temp2 t2 on t2.SettingKey=td.SettingKey;    

                    Select * from ##Temp1
                    Select * from ##temp2";
        public const string PHASEI_QueryTemplateForRoleManagement = @"IF OBJECT_ID('tempdb.dbo.##Temp1', 'U') IS NOT NULL
                      DROP TABLE ##Temp1; 
                    IF OBJECT_ID('tempdb.dbo.##Temp2', 'U') IS NOT NULL
                      DROP TABLE ##Temp2; 
				

                    ;With OrgHierarchy As 
                    ( 
                    Select O.OrganizationPK,O.Name, O.ParentOrganizationFK, CAST(O.Name as nvarchar(4000)) as Depth
                    From Organization O
                    Where O.OrganizationPK IN (select OrganizationPK from Organization where OrgDomainInd = 1 and isnull(ParentOrganizationFK,'') = '')
                    Union All 
                    Select O2.OrganizationPK,O2.Name, O2.ParentOrganizationFK, Depth+' --> ' + O2.Name
                    From Organization O2
                        Join OrgHierarchy 
                            On OrgHierarchy.OrganizationPK = O2.ParentOrganizationFK 
                    ) 
                    select ROW_NUMBER() OVER(PARTITION BY OrganizationPK  ORDER BY OrganizationPK ASC) row,ir.*  
                   -- replace(p.Section,'Domain/'+Cast(o.OrganizationPK as nvarchar(9))+'/','') as CSec, replace(Data,char(13)+char(10),@delimeter) as Data1, *  
                   into  ##Temp1  
                    from iwc_Role ir
                    inner join OrgHierarchy O on ir.Role_DomainFK=o.OrganizationPK 
                    where 1=1 AND (Role_RoleMask = 1 OR Role_DefaultInd=1)
                    
                    								
	                SELECT  temp.Role_DomainFK, 
                          RolePerm_Name as PermName,   
                          RolePerm_Value as PermValue,temp.Role_PK,temp.Role_Name,temp.Role_RoleMask 
		                  Into ##Temp2
                          FROM    iwc_RolePerm   irp
		                  Inner join ##Temp1 temp ON irp.RolePerm_RoleFK=temp.Role_PK        
                          ORDER BY   
                          RolePerm_Name    		 

                Select * from ##Temp2 where Role_DomainFK IN(104,217)
                Select OrganizationPK,Name from organization where OrgDomainInd=1;";

    }
}
