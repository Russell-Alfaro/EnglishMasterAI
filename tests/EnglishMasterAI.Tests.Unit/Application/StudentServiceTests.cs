using EnglishMasterAI.Application.DTOs;
using EnglishMasterAI.Application.Ports.Output;
using EnglishMasterAI.Application.Services;
using EnglishMasterAI.Domain.Entities;
using EnglishMasterAI.Domain.Exceptions;
using Moq;
using Xunit;

namespace EnglishMasterAI.Tests.Unit.Application;

/// <summary>
/// Suite de pruebas unitarias exhaustiva para <see cref="StudentService"/>
/// y las entidades del Dominio (<see cref="Student"/>).
///
/// ESTRATEGIAS APLICADAS:
///   - Boundary Value Analysis (BVA): valores en los límites exactos de las reglas
///   - Equivalence Partitioning (EP): clases de equivalencia válidas e inválidas
///   - [Theory] + [InlineData] / [MemberData]: parametrización masiva para ~100 casos
///
/// HERRAMIENTAS:
///   - xUnit  : Framework de pruebas (Theory, Fact, InlineData, MemberData)
///   - Moq    : MockBehavior.Strict para IStudentRepository
///
/// CONVENCIÓN DE NOMBRES:
///   MetodoQueSeProbar_Escenario_ResultadoEsperado
/// </summary>
public class StudentServiceTests
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Fixtures compartidos (constructor = SetUp en xUnit)
    // ─────────────────────────────────────────────────────────────────────────

    private readonly Mock<IStudentRepository> _repositoryMock;
    private readonly StudentService _sut; // SUT = System Under Test

    public StudentServiceTests()
    {
        // MockBehavior.Strict → cualquier llamada NO configurada explota el test
        _repositoryMock = new Mock<IStudentRepository>(MockBehavior.Strict);
        _sut = new StudentService(_repositoryMock.Object);
    }

    // =========================================================================
    //  BLOQUE A ─ Registro Exitoso (camino feliz parametrizado) [8 casos]
    // =========================================================================

    /// <summary>
    /// EP: partición válida de correos — diferentes dominios y formatos correctos.
    /// Verifica que el servicio persiste y devuelve el DTO correcto para cada variante.
    /// </summary>
    [Theory]
    [Trait("Category", "Registration_HappyPath")]
    [InlineData("Ana García",      "ana@gmail.com",                    "Spanish")]
    [InlineData("Bob Smith",       "bob.smith@university.edu",         "English")]
    [InlineData("María López",     "m.lopez@empresa.com.mx",           "Spanish")]
    [InlineData("Chen Wei",        "chen.wei@pku.edu.cn",              "Mandarin")]
    [InlineData("Ivan Ivanov",     "ivan@mail.ru",                     "Russian")]
    [InlineData("Fatima Al-Said",  "fatima+tag@outlook.co.uk",         "Arabic")]
    [InlineData("Test User",       "user123@subdomain.example.org",    "French")]
    [InlineData("Unico Nombre",    "unico_nombre@correo.io",           "Spanish")]
    public async Task RegisterStudentAsync_WithValidData_ReturnsMappedDto(
        string fullName, string email, string nativeLanguage)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        var command = new RegisterStudentCommand(fullName, email, "SecurePass1!", nativeLanguage);

        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student s, CancellationToken _) => s);

        // ── ACT ───────────────────────────────────────────────────────────────
        StudentDto result = await _sut.RegisterStudentAsync(command);

        // ── ASSERT ────────────────────────────────────────────────────────────
        Assert.NotNull(result);
        Assert.Equal(fullName,                     result.FullName);
        Assert.Equal(email.ToLowerInvariant(),     result.Email);
        Assert.Equal(nativeLanguage,               result.NativeLanguage);
        Assert.Equal(0,                            result.CurrentLevelScore);
        Assert.Equal(0,                            result.TotalLessonsCompleted);
        Assert.Equal(0,                            result.TotalPracticeMinutes);
        Assert.True(result.IsActive);
        Assert.NotEqual(Guid.Empty,                result.Id);
        Assert.Null(result.LastActivityAt);

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================================
    //  BLOQUE B ─ Correos Inválidos (BVA + EP sobre formato de email) [11 casos]
    // =========================================================================

    /// <summary>
    /// EP: partición inválida de emails — el dominio rechaza correos sin '@'.
    /// Student.Create lanza ArgumentException antes de llegar al repositorio.
    /// </summary>
    [Theory]
    [Trait("Category", "Registration_InvalidEmail")]
    // Sin arroba
    [InlineData("noatsign.com")]
    [InlineData("plainaddress")]
    [InlineData("missingatdomain")]
    // Espacios en blanco / vacíos
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("")]
    // Casos límite: cadena con sólo caracteres no válidos como correo
    [InlineData("domain-only.com")]
    [InlineData("user.without.at")]
    [InlineData("nodot_noat")]
    public async Task RegisterStudentAsync_WithInvalidEmail_ThrowsArgumentException(string invalidEmail)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        // El servicio llama ExistsByEmailAsync ANTES de Student.Create,
        // por tanto configuramos esa llamada (con el email inválido).
        // Después Student.Create lanzará al validar el '@'.
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync(invalidEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new RegisterStudentCommand("Nombre Valido", invalidEmail, "Password1!");

        // ── ACT & ASSERT ──────────────────────────────────────────────────────
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.RegisterStudentAsync(command));

        // Nunca debe persistir
        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // =========================================================================
    //  BLOQUE C ─ Nombres Inválidos (BVA: límites del nombre) [6 + 6 = 12 casos]
    // =========================================================================

    /// <summary>
    /// EP: partición inválida de fullName (vacío o solo espacios).
    /// </summary>
    [Theory]
    [Trait("Category", "Registration_InvalidName")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task RegisterStudentAsync_WithBlankName_ThrowsArgumentException(string blankName)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        const string validEmail = "valido@correo.com";
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync(validEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new RegisterStudentCommand(blankName, validEmail, "Password1!");

        // ── ACT & ASSERT ──────────────────────────────────────────────────────
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.RegisterStudentAsync(command));

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// BVA + EP: nombres válidos en los límites — 1 carácter, 2, con caracteres
    /// especiales permitidos, nombres muy largos (sin cap en dominio).
    /// </summary>
    [Theory]
    [Trait("Category", "Registration_ValidName")]
    [InlineData("A")]
    [InlineData("Ab")]
    [InlineData("Maria Jose")]
    [InlineData("O'Brien")]
    [InlineData("Garcia-Perez")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task RegisterStudentAsync_WithValidName_Succeeds(string validName)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        const string email = "test@ejemplo.com";
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student s, CancellationToken _) => s);

        var command = new RegisterStudentCommand(validName, email, "SecurePass1!");

        // ── ACT ───────────────────────────────────────────────────────────────
        StudentDto result = await _sut.RegisterStudentAsync(command);

        // ── ASSERT ────────────────────────────────────────────────────────────
        Assert.NotNull(result);
        Assert.Equal(validName, result.FullName);
    }

    // =========================================================================
    //  BLOQUE D ─ Contraseñas Inválidas a nivel de Dominio [5 casos]
    // =========================================================================

    /// <summary>
    /// EP: partición inválida de contraseña — vacías o solo espacios.
    /// Se prueba directamente en Student.Create (nivel de dominio),
    /// porque SHA-256("") produce un hash no vacío y el servicio no valida
    /// la fortaleza de la contraseña en texto plano.
    /// </summary>
    [Theory]
    [Trait("Category", "Domain_InvalidPasswordHash")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void StudentCreate_WithBlankPasswordHash_ThrowsArgumentException(string blankHash)
    {
        // ── ACT & ASSERT ──────────────────────────────────────────────────────
        var ex = Assert.Throws<ArgumentException>(
            () => Student.Create("Nombre", "valido@correo.com", blankHash));
        Assert.Equal("passwordHash", ex.ParamName);
    }

    // =========================================================================
    //  BLOQUE E ─ Manejo de Duplicados [6 casos]
    // =========================================================================

    /// <summary>
    /// EP: intentos de registrar el mismo correo — con variaciones de mayúsculas.
    /// El servicio llama ExistsByEmailAsync con el email tal como lo recibe.
    /// </summary>
    [Theory]
    [Trait("Category", "Registration_DuplicateEmail")]
    [InlineData("duplicado@test.com")]
    [InlineData("DUPLICADO@TEST.COM")]
    [InlineData("Duplicado@Test.Com")]
    [InlineData("DuPlIcAdO@TeSt.CoM")]
    [InlineData("alumno.existente@universidad.edu.mx")]
    [InlineData("ya.registrado+tag@gmail.com")]
    public async Task RegisterStudentAsync_WithDuplicateEmail_ThrowsDuplicateEmailException(string email)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new RegisterStudentCommand("Otro Nombre", email, "Password1!");

        // ── ACT & ASSERT ──────────────────────────────────────────────────────
        DuplicateEmailException ex = await Assert.ThrowsAsync<DuplicateEmailException>(
            () => _sut.RegisterStudentAsync(command));

        Assert.Equal(email, ex.Email);
        Assert.Contains(email, ex.Message);

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // =========================================================================
    //  BLOQUE F ─ GetStudentByIdAsync: ID existente [1 caso Fact]
    // =========================================================================

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetStudentByIdAsync_WithExistingId_ReturnsCorrectDto()
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        var student = Student.Create("Pedro Alvarado", "pedro@tec.mx", "hashSecure");
        Guid id = student.Id;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // ── ACT ───────────────────────────────────────────────────────────────
        StudentDto result = await _sut.GetStudentByIdAsync(id);

        // ── ASSERT ────────────────────────────────────────────────────────────
        Assert.NotNull(result);
        Assert.Equal(id,               result.Id);
        Assert.Equal("Pedro Alvarado", result.FullName);
        Assert.Equal("pedro@tec.mx",  result.Email);
        Assert.True(result.IsActive);

        _repositoryMock.Verify(
            r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================================
    //  BLOQUE G ─ GetStudentByIdAsync: ID inexistente [7 casos MemberData]
    // =========================================================================

    public static TheoryData<Guid> NonExistentGuids => new()
    {
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        new Guid("00000000-0000-0000-0000-000000000001"),
        new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"),
    };

    [Theory]
    [Trait("Category", "Query_NotFound")]
    [MemberData(nameof(NonExistentGuids))]
    public async Task GetStudentByIdAsync_WithNonExistentId_ThrowsStudentNotFoundException(Guid nonExistentId)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        _repositoryMock
            .Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        // ── ACT & ASSERT ──────────────────────────────────────────────────────
        StudentNotFoundException ex = await Assert.ThrowsAsync<StudentNotFoundException>(
            () => _sut.GetStudentByIdAsync(nonExistentId));

        Assert.Equal(nonExistentId, ex.StudentId);
        Assert.Contains(nonExistentId.ToString(), ex.Message);
    }

    // =========================================================================
    //  BLOQUE H ─ GetAllStudentsAsync: tamaño de lista [6 casos]
    // =========================================================================

    [Theory]
    [Trait("Category", "Query")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    public async Task GetAllStudentsAsync_ReturnsCorrectCount(int count)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        var students = Enumerable.Range(1, count)
            .Select(i => Student.Create($"Estudiante {i}", $"e{i}@test.com", $"hash{i}"))
            .ToList()
            .AsReadOnly();

        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(students);

        // ── ACT ───────────────────────────────────────────────────────────────
        IReadOnlyList<StudentDto> result = await _sut.GetAllStudentsAsync();

        // ── ASSERT ────────────────────────────────────────────────────────────
        Assert.NotNull(result);
        Assert.Equal(count, result.Count);
        Assert.All(result, dto => Assert.True(dto.IsActive));

        _repositoryMock.Verify(
            r => r.GetAllAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =========================================================================
    //  BLOQUE I ─ Dashboard: niveles de suficiencia (BVA en umbrales) [19 casos]
    // =========================================================================

    /// <summary>
    /// BVA: valores exactamente en los límites de los rangos del switch de nivel.
    /// Verifica CalculateProficiencyLevel y CalculateProgressPercentage.
    /// </summary>
    [Theory]
    [Trait("Category", "Dashboard_ProficiencyLevel")]
    // score → nivel esperado                → progreso %
    [InlineData(0,    "A1 - Principiante",    0.00)]
    [InlineData(1,    "A1 - Principiante",    0.08)]
    [InlineData(99,   "A1 - Principiante",    8.25)]
    [InlineData(100,  "A2 - Básico",          8.33)]
    [InlineData(101,  "A2 - Básico",          8.42)]
    [InlineData(249,  "A2 - Básico",         20.75)]
    [InlineData(250,  "B1 - Intermedio",     20.83)]
    [InlineData(251,  "B1 - Intermedio",     20.92)]
    [InlineData(499,  "B1 - Intermedio",     41.58)]
    [InlineData(500,  "B2 - Intermedio Alto", 41.67)]
    [InlineData(501,  "B2 - Intermedio Alto", 41.75)]
    [InlineData(799,  "B2 - Intermedio Alto", 66.58)]
    [InlineData(800,  "C1 - Avanzado",       66.67)]
    [InlineData(801,  "C1 - Avanzado",       66.75)]
    [InlineData(1099, "C1 - Avanzado",       91.58)]
    [InlineData(1100, "C2 - Maestría",       91.67)]
    [InlineData(1200, "C2 - Maestría",      100.00)]
    [InlineData(1500, "C2 - Maestría",      100.00)] // capped al 100 %
    [InlineData(9999, "C2 - Maestría",      100.00)] // valor extremo
    public async Task GetDashboardAsync_WithScore_ReturnsCorrectLevelAndProgress(
        int score, string expectedLevel, double expectedProgress)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        var student = Student.Create("Dashboard Tester", "dash@test.com", "hash");

        if (score > 0)
            student.CompleteLesson(score, 1);

        Guid id = student.Id;
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // ── ACT ───────────────────────────────────────────────────────────────
        StudentDashboardDto result = await _sut.GetDashboardAsync(id);

        // ── ASSERT ────────────────────────────────────────────────────────────
        Assert.Equal(expectedLevel,    result.ProficiencyLevel);
        Assert.Equal(expectedProgress, result.ProgressPercentage, precision: 1);
        Assert.Equal(id,               result.StudentId);
    }

    // =========================================================================
    //  BLOQUE J ─ Dominio: Student.Create — partición válida [3 casos]
    // =========================================================================

    [Theory]
    [Trait("Category", "Domain_StudentCreate_Valid")]
    [InlineData("A",             "a@b.com",       "hash")]
    [InlineData("Nombre Largo",  "correo@org.net","cualquierHash")]
    [InlineData("Test",          "zh@dominio.cn", "hash_zh")]
    public void StudentCreate_WithValidData_ReturnsStudent(string name, string email, string hash)
    {
        // ── ACT ───────────────────────────────────────────────────────────────
        Student student = Student.Create(name, email, hash);

        // ── ASSERT ────────────────────────────────────────────────────────────
        Assert.NotNull(student);
        Assert.Equal(name,                         student.FullName);
        Assert.Equal(email.Trim().ToLowerInvariant(), student.Email);
        Assert.Equal(0,                            student.CurrentLevelScore);
        Assert.Equal(0,                            student.TotalLessonsCompleted);
        Assert.Equal(0,                            student.TotalPracticeMinutes);
        Assert.True(student.IsActive);
        Assert.NotEqual(Guid.Empty,                student.Id);
        Assert.Null(student.LastActivityAt);
    }

    // =========================================================================
    //  BLOQUE K ─ Dominio: Student.Create — nombre inválido [4 casos]
    // =========================================================================

    [Theory]
    [Trait("Category", "Domain_StudentCreate_Invalid")]
    [InlineData("",   "valido@correo.com", "hash")]
    [InlineData(" ",  "valido@correo.com", "hash")]
    [InlineData("\t", "valido@correo.com", "hash")]
    [InlineData("\n", "valido@correo.com", "hash")]
    public void StudentCreate_WithBlankName_ThrowsArgumentException(string name, string email, string hash)
    {
        var ex = Assert.Throws<ArgumentException>(() => Student.Create(name, email, hash));
        Assert.Equal("fullName", ex.ParamName);
    }

    // =========================================================================
    //  BLOQUE L ─ Dominio: Student.Create — email inválido [6 casos]
    // =========================================================================

    [Theory]
    [Trait("Category", "Domain_StudentCreate_Invalid")]
    [InlineData("Nombre", "",              "hash")]
    [InlineData("Nombre", " ",             "hash")]
    [InlineData("Nombre", "sinArrobacom",  "hash")]
    [InlineData("Nombre", "invalido",      "hash")]
    [InlineData("Nombre", "punto.solo",    "hash")]
    [InlineData("Nombre", "domain-only",   "hash")]
    public void StudentCreate_WithInvalidEmail_ThrowsArgumentException(string name, string email, string hash)
    {
        var ex = Assert.Throws<ArgumentException>(() => Student.Create(name, email, hash));
        Assert.Equal("email", ex.ParamName);
    }

    // =========================================================================
    //  BLOQUE M ─ Dominio: CompleteLesson — valores válidos [8 casos]
    // =========================================================================

    [Theory]
    [Trait("Category", "Domain_CompleteLesson_Valid")]
    // scoreGained, minutesPracticed → expectedScore, expectedLessons, expectedMinutes
    [InlineData(0,      1,     0,    1,   1)]   // Score 0 es válido
    [InlineData(1,      1,     1,    1,   1)]   // Mínimos positivos
    [InlineData(10,     5,    10,    1,   5)]
    [InlineData(50,    30,    50,    1,  30)]
    [InlineData(100,   60,   100,    1,  60)]
    [InlineData(500,  120,   500,    1, 120)]
    [InlineData(1000, 180,  1000,    1, 180)]
    [InlineData(9999, 999,  9999,    1, 999)]   // Extremadamente altos (sin cap)
    public void CompleteLesson_WithValidValues_UpdatesProgress(
        int score, int minutes,
        int expectedScore, int expectedLessons, int expectedMinutes)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        var student = Student.Create("Tester", "tester@test.com", "hash");

        // ── ACT ───────────────────────────────────────────────────────────────
        student.CompleteLesson(score, minutes);

        // ── ASSERT ────────────────────────────────────────────────────────────
        Assert.Equal(expectedScore,   student.CurrentLevelScore);
        Assert.Equal(expectedLessons, student.TotalLessonsCompleted);
        Assert.Equal(expectedMinutes, student.TotalPracticeMinutes);
        Assert.NotNull(student.LastActivityAt);
    }

    // =========================================================================
    //  BLOQUE N ─ Dominio: Acumulación de lecciones [4 casos]
    // =========================================================================

    [Theory]
    [Trait("Category", "Domain_CompleteLesson_Accumulation")]
    // score1, min1, score2, min2 → totalScore, totalLessons, totalMinutes
    [InlineData(10, 5,  20, 10,  30, 2,  15)]
    [InlineData(50, 30, 50, 30, 100, 2,  60)]
    [InlineData(0,  1,   0,  1,   0, 2,   2)]
    [InlineData(100,60, 200,90, 300, 2, 150)]
    public void CompleteLesson_CalledTwice_AccumulatesTotals(
        int score1, int min1, int score2, int min2,
        int totalScore, int totalLessons, int totalMinutes)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        var student = Student.Create("Acumulador", "acum@test.com", "hash");

        // ── ACT ───────────────────────────────────────────────────────────────
        student.CompleteLesson(score1, min1);
        student.CompleteLesson(score2, min2);

        // ── ASSERT ────────────────────────────────────────────────────────────
        Assert.Equal(totalScore,   student.CurrentLevelScore);
        Assert.Equal(totalLessons, student.TotalLessonsCompleted);
        Assert.Equal(totalMinutes, student.TotalPracticeMinutes);
    }

    // =========================================================================
    //  BLOQUE O ─ Dominio: CompleteLesson con score negativo [4 casos BVA]
    // =========================================================================

    [Theory]
    [Trait("Category", "Domain_CompleteLesson_Invalid")]
    [InlineData(-1,   1)]             // BVA: un punto por debajo del mínimo
    [InlineData(-10,  5)]
    [InlineData(-100, 60)]
    [InlineData(int.MinValue, 1)]     // Extremo absoluto negativo
    public void CompleteLesson_WithNegativeScore_ThrowsArgumentException(int negativeScore, int minutes)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        var student = Student.Create("Tester", "tester@test.com", "hash");

        // ── ACT & ASSERT ──────────────────────────────────────────────────────
        Assert.Throws<ArgumentException>(() => student.CompleteLesson(negativeScore, minutes));

        // Estado NO debe haber cambiado
        Assert.Equal(0, student.CurrentLevelScore);
        Assert.Equal(0, student.TotalLessonsCompleted);
        Assert.Equal(0, student.TotalPracticeMinutes);
    }

    // =========================================================================
    //  BLOQUE P ─ Dominio: CompleteLesson con minutos ≤ 0 [4 casos BVA]
    // =========================================================================

    [Theory]
    [Trait("Category", "Domain_CompleteLesson_Invalid")]
    [InlineData(10,  0)]              // BVA: exactamente cero (no positivo)
    [InlineData(10, -1)]              // BVA: un punto debajo del mínimo
    [InlineData(10, -60)]
    [InlineData(10, int.MinValue)]
    public void CompleteLesson_WithZeroOrNegativeMinutes_ThrowsArgumentException(int score, int invalidMinutes)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        var student = Student.Create("Tester", "tester@test.com", "hash");

        // ── ACT & ASSERT ──────────────────────────────────────────────────────
        Assert.Throws<ArgumentException>(() => student.CompleteLesson(score, invalidMinutes));

        // Estado intacto
        Assert.Equal(0, student.CurrentLevelScore);
    }

    // =========================================================================
    //  BLOQUE Q ─ Dominio: Student.Deactivate — gestión de estado [3 casos]
    // =========================================================================

    [Fact]
    [Trait("Category", "Domain_Deactivate")]
    public void Deactivate_WhenStudentIsActive_SetsIsActiveFalse()
    {
        var student = Student.Create("Activo", "activo@test.com", "hash");
        Assert.True(student.IsActive);

        student.Deactivate();

        Assert.False(student.IsActive);
    }

    [Fact]
    [Trait("Category", "Domain_Deactivate")]
    public void Deactivate_WhenAlreadyInactive_RemainsInactive()
    {
        var student = Student.Create("Inactivo", "inactivo@test.com", "hash");
        student.Deactivate();
        Assert.False(student.IsActive);

        student.Deactivate(); // Segunda llamada — idempotente

        Assert.False(student.IsActive);
    }

    [Fact]
    [Trait("Category", "Domain_Deactivate")]
    public void Deactivate_DoesNotAffectProgressProperties()
    {
        var student = Student.Create("Completo", "completo@test.com", "hash");
        student.CompleteLesson(50, 30);
        int scoreBefore   = student.CurrentLevelScore;
        int lessonsBefore = student.TotalLessonsCompleted;
        int minutesBefore = student.TotalPracticeMinutes;

        student.Deactivate();

        Assert.False(student.IsActive);
        Assert.Equal(scoreBefore,   student.CurrentLevelScore);
        Assert.Equal(lessonsBefore, student.TotalLessonsCompleted);
        Assert.Equal(minutesBefore, student.TotalPracticeMinutes);
    }

    // =========================================================================
    //  BLOQUE R ─ Dominio: LastActivityAt [2 casos Fact]
    // =========================================================================

    [Fact]
    [Trait("Category", "Domain_LastActivity")]
    public void CompleteLesson_SetsLastActivityAtToApproximatelyNow()
    {
        var student = Student.Create("Activity", "activity@test.com", "hash");
        Assert.Null(student.LastActivityAt);

        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        student.CompleteLesson(10, 5);
        DateTime after  = DateTime.UtcNow.AddSeconds(1);

        Assert.NotNull(student.LastActivityAt);
        Assert.InRange(student.LastActivityAt!.Value, before, after);
    }

    [Fact]
    [Trait("Category", "Domain_LastActivity")]
    public void CompleteLesson_CalledMultipleTimes_UpdatesLastActivityAtEachTime()
    {
        var student = Student.Create("Multi", "multi@test.com", "hash");
        student.CompleteLesson(10, 5);
        DateTime? firstActivity = student.LastActivityAt;

        System.Threading.Thread.Sleep(10);
        student.CompleteLesson(20, 10);

        Assert.NotNull(student.LastActivityAt);
        Assert.True(student.LastActivityAt >= firstActivity);
    }

    // =========================================================================
    //  BLOQUE S ─ Service: GetDashboardAsync con ID inexistente [7 MemberData]
    // =========================================================================

    [Theory]
    [Trait("Category", "Dashboard_NotFound")]
    [MemberData(nameof(NonExistentGuids))]
    public async Task GetDashboardAsync_WithNonExistentId_ThrowsStudentNotFoundException(Guid nonExistentId)
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        StudentNotFoundException ex = await Assert.ThrowsAsync<StudentNotFoundException>(
            () => _sut.GetDashboardAsync(nonExistentId));

        Assert.Equal(nonExistentId, ex.StudentId);
    }

    // =========================================================================
    //  BLOQUE T ─ Service: Dashboard refleja progreso acumulado [5 casos]
    // =========================================================================

    [Theory]
    [Trait("Category", "Dashboard_Progress")]
    // numLessons, scorePerLesson, minutesPerLesson → totalLessons, totalMinutes, totalScore
    [InlineData(1,  10,  5,   1,   5,   10)]
    [InlineData(3,  20, 15,   3,  45,   60)]
    [InlineData(5,  50, 30,   5, 150,  250)]
    [InlineData(10,100, 60,  10, 600, 1000)]
    [InlineData(0,   0,  0,   0,   0,    0)]
    public async Task GetDashboardAsync_ReflectsAccumulatedProgress(
        int numLessons, int scorePerLesson, int minutesPerLesson,
        int expectedLessons, int expectedMinutes, int expectedScore)
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        var student = Student.Create("Progresista", "progreso@test.com", "hash");

        for (int i = 0; i < numLessons; i++)
            if (minutesPerLesson > 0)
                student.CompleteLesson(scorePerLesson, minutesPerLesson);

        Guid id = student.Id;
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // ── ACT ───────────────────────────────────────────────────────────────
        StudentDashboardDto dashboard = await _sut.GetDashboardAsync(id);

        // ── ASSERT ────────────────────────────────────────────────────────────
        Assert.Equal(expectedLessons, dashboard.TotalLessonsCompleted);
        Assert.Equal(expectedMinutes, dashboard.TotalPracticeMinutes);
        Assert.Equal(expectedScore,   dashboard.CurrentLevelScore);
        Assert.Equal("Progresista",   dashboard.FullName);
    }

    // =========================================================================
    //  BLOQUE U ─ Dominio: NativeLanguage — 8 idiomas explícitos + 1 por defecto
    // =========================================================================

    [Theory]
    [Trait("Category", "Domain_NativeLanguage")]
    [InlineData("Spanish")]
    [InlineData("English")]
    [InlineData("Mandarin")]
    [InlineData("French")]
    [InlineData("Portuguese")]
    [InlineData("German")]
    [InlineData("Japanese")]
    [InlineData("Arabic")]
    public void StudentCreate_WithExplicitNativeLanguage_StoresLanguageCorrectly(string language)
    {
        var student = Student.Create("Nombre", "idioma@test.com", "hash", language);
        Assert.Equal(language, student.NativeLanguage);
    }

    [Fact]
    [Trait("Category", "Domain_NativeLanguage")]
    public void StudentCreate_WithDefaultNativeLanguage_IsSpanish()
    {
        var student = Student.Create("Nombre", "default@test.com", "hash");
        Assert.Equal("Spanish", student.NativeLanguage);
    }

    // =========================================================================
    //  BLOQUE V ─ Dominio: Email normalizado a minúsculas [4 casos]
    // =========================================================================

    [Theory]
    [Trait("Category", "Domain_EmailNormalization")]
    [InlineData("USER@DOMAIN.COM",          "user@domain.com")]
    [InlineData("Mixed.Case@Example.Org",   "mixed.case@example.org")]
    [InlineData("  leading@space.com  ",    "leading@space.com")]
    [InlineData("UPPER@LOWER.com",          "upper@lower.com")]
    public void StudentCreate_EmailIsNormalizedToLowercase(string inputEmail, string expectedEmail)
    {
        var student = Student.Create("Normalizador", inputEmail, "hash");
        Assert.Equal(expectedEmail, student.Email);
    }

    // =========================================================================
    //  BLOQUE W ─ Dominio: IDs únicos por instancia [1 Fact con 50 instancias]
    // =========================================================================

    [Fact]
    [Trait("Category", "Domain_UniqueId")]
    public void StudentCreate_MultipleInstances_HaveUniqueIds()
    {
        var ids = Enumerable.Range(1, 50)
            .Select(i => Student.Create($"Student {i}", $"s{i}@test.com", "hash").Id)
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.NotEqual(Guid.Empty, id));
    }

    // =========================================================================
    //  BLOQUE X ─ Service: Constructor con repositorio nulo [1 caso]
    // =========================================================================

    [Fact]
    [Trait("Category", "Service_Constructor")]
    public void StudentService_WithNullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new StudentService(null!));
    }

    // =========================================================================
    //  BLOQUE Y ─ Service: CancellationToken cancelado propaga excepción [1 caso]
    // =========================================================================

    [Fact]
    [Trait("Category", "Service_Cancellation")]
    public async Task RegisterStudentAsync_WhenRepositoryThrowsOnCancel_PropagatesException()
    {
        // ── ARRANGE ──────────────────────────────────────────────────────────
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        const string email = "cancel@test.com";
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var command = new RegisterStudentCommand("Cancel User", email, "Password1!");

        // ── ACT & ASSERT ──────────────────────────────────────────────────────
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.RegisterStudentAsync(command, cts.Token));
    }
}
