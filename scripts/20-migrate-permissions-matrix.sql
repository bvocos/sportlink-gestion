/* Convierte el array plano histórico a la matriz de permisos por módulo y acción. */
SET NOCOUNT ON;
DECLARE @Id uniqueidentifier, @Anterior nvarchar(max), @Matriz nvarchar(max);
DECLARE permisos_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT Id, PermisosJson FROM dbo.Usuarios WHERE ISJSON(PermisosJson)=1 AND LEFT(LTRIM(PermisosJson),1)='[';
OPEN permisos_cursor; FETCH NEXT FROM permisos_cursor INTO @Id,@Anterior;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @Matriz=N'{}';
 IF EXISTS(SELECT 1 FROM OPENJSON(@Anterior) WHERE value=N'dashboard') SET @Matriz=JSON_MODIFY(@Matriz,'$.dashboard',JSON_QUERY(N'{"ver":true}'));
 IF EXISTS(SELECT 1 FROM OPENJSON(@Anterior) WHERE value=N'ventas') BEGIN SET @Matriz=JSON_MODIFY(@Matriz,'$.ventas',JSON_QUERY(N'{"ver":true,"crear":true,"editar":true,"eliminar":true}')); SET @Matriz=JSON_MODIFY(@Matriz,'$."ventas.verMontos"',CAST(1 AS bit)); END;
 IF EXISTS(SELECT 1 FROM OPENJSON(@Anterior) WHERE value=N'entregas') SET @Matriz=JSON_MODIFY(@Matriz,'$.entregas',JSON_QUERY(N'{"ver":true}'));
 IF EXISTS(SELECT 1 FROM OPENJSON(@Anterior) WHERE value=N'presupuestos') SET @Matriz=JSON_MODIFY(@Matriz,'$.presupuestos',JSON_QUERY(N'{"ver":true,"crear":true,"editar":true,"eliminar":true}'));
 IF EXISTS(SELECT 1 FROM OPENJSON(@Anterior) WHERE value=N'clientes') SET @Matriz=JSON_MODIFY(@Matriz,'$.clientes',JSON_QUERY(N'{"ver":true,"crear":true,"editar":true,"eliminar":true}'));
 IF EXISTS(SELECT 1 FROM OPENJSON(@Anterior) WHERE value=N'cuotas') BEGIN SET @Matriz=JSON_MODIFY(@Matriz,'$.cuotas',JSON_QUERY(N'{"ver":true,"editar":true,"registrarPago":true,"anularPago":true}')); SET @Matriz=JSON_MODIFY(@Matriz,'$."cuotas.verMontos"',CAST(1 AS bit)); END;
 IF EXISTS(SELECT 1 FROM OPENJSON(@Anterior) WHERE value=N'caja') SET @Matriz=JSON_MODIFY(@Matriz,'$.caja',JSON_QUERY(N'{"ver":true,"crear":true,"editar":true}'));
 IF EXISTS(SELECT 1 FROM OPENJSON(@Anterior) WHERE value=N'gastos') SET @Matriz=JSON_MODIFY(@Matriz,'$.gastos',JSON_QUERY(N'{"ver":true,"crear":true,"editar":true,"eliminar":true}'));
 IF EXISTS(SELECT 1 FROM OPENJSON(@Anterior) WHERE value=N'rentabilidad') SET @Matriz=JSON_MODIFY(@Matriz,'$.rentabilidad',JSON_QUERY(N'{"ver":true}'));
 IF EXISTS(SELECT 1 FROM OPENJSON(@Anterior) WHERE value=N'stock') SET @Matriz=JSON_MODIFY(@Matriz,'$.stock',JSON_QUERY(N'{"ver":true,"crear":true}'));
 IF EXISTS(SELECT 1 FROM OPENJSON(@Anterior) WHERE value=N'administracion') SET @Matriz=JSON_MODIFY(@Matriz,'$.administracion',JSON_QUERY(N'{"ver":true,"crear":true,"editar":true}'));
 UPDATE dbo.Usuarios SET PermisosJson=@Matriz WHERE Id=@Id;
 FETCH NEXT FROM permisos_cursor INTO @Id,@Anterior;
END
CLOSE permisos_cursor; DEALLOCATE permisos_cursor;
